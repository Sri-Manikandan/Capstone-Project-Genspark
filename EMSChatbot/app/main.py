from __future__ import annotations
import json
import os
from fastapi import FastAPI, Header, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from langgraph.checkpoint.memory import MemorySaver

from app.config import get_settings
from app.auth import validate_token
from app.llm import build_llm
from app.tools import build_tools
from app.agent import build_agent
from app.ems_client import EmsClient, EmsAuthError, EmsApiError
from app.schemas import ChatRequest

app = FastAPI(title="EMS Chatbot")
_memory = MemorySaver()

# In production the SPA (Static Web Apps) and this service (AKS ingress) are on
# different origins, so the browser needs CORS. Read from the environment directly
# (not Settings) so importing this module never requires the full config; when the
# variable is unset (dev, tests — same-origin via the Angular proxy) no CORS
# middleware is added at all.
_cors_origins = [o.strip() for o in os.environ.get("CORS_ALLOWED_ORIGINS", "").split(",") if o.strip()]
if _cors_origins:
    app.add_middleware(
        CORSMiddleware,
        allow_origins=_cors_origins,
        allow_methods=["POST"],
        allow_headers=["Authorization", "Content-Type"],
    )


@app.get("/healthz")
@app.get("/ai/healthz")
async def healthz():
    # /healthz for pod probes; /ai/healthz is reachable through the ingress /ai
    # path for external smoke tests.
    return {"status": "ok"}


def _sse(obj: dict) -> str:
    return f"data: {json.dumps(obj)}\n\n"


def _chunk_text(chunk) -> str:
    """Extract streamed text from a model chunk.

    Without tools bound, langchain-anthropic streams plain-string content; with
    tools bound (as in our agent) it streams a LIST of content blocks like
    [{"type": "text", "text": "...", "index": 0}]. Handle both — the string-only
    path silently dropped every token in production.
    """
    content = getattr(chunk, "content", "")
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "".join(
            block.get("text", "")
            for block in content
            if isinstance(block, dict) and block.get("type") == "text"
        )
    return ""


@app.post("/ai/chat")
async def chat(req: ChatRequest, authorization: str | None = Header(default=None)):
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    jwt = authorization[len("Bearer "):].strip()
    if not jwt:
        raise HTTPException(status_code=401, detail="Missing bearer token")
    settings = get_settings()
    # Validate the token locally BEFORE touching the LLM gateway so a missing or
    # invalid token is rejected with 401 without incurring model cost.
    claims = validate_token(jwt, settings)
    user_id = claims["sub"]

    async def stream():
        ems = EmsClient(base_url=settings.ems_api_base_url, jwt=jwt)
        try:
            llm = build_llm(settings)
            agent = build_agent(llm, build_tools(ems), _memory)
            # NOTE: namespace the thread by authenticated user so one caller can't
            # load another session's history. In-memory MemorySaver grows unbounded
            # here — accepted limitation for this in-memory iteration (no eviction).
            thread_id = f"{user_id}:{req.conversation_id}"
            config = {"configurable": {"thread_id": thread_id}}
            async for event in agent.astream_events(
                {"messages": [("user", req.message)]}, config=config, version="v2"
            ):
                kind = event["event"]
                if kind == "on_chat_model_stream":
                    text = _chunk_text(event["data"]["chunk"])
                    if text:
                        yield _sse({"type": "token", "text": text})
                elif kind == "on_tool_start":
                    yield _sse({"type": "tool", "name": event.get("name", "")})
            yield _sse({"type": "done"})
        except EmsAuthError:
            yield _sse({"type": "error", "text": "session expired, please log in again"})
            yield _sse({"type": "done"})
        except EmsApiError as e:
            yield _sse({"type": "error", "text": f"Something went wrong: {e.message}"})
            yield _sse({"type": "done"})
        except Exception:
            yield _sse({"type": "error", "text": "Sorry, I hit a problem. Please try again."})
            yield _sse({"type": "done"})
        finally:
            await ems.aclose()

    return StreamingResponse(stream(), media_type="text/event-stream")
