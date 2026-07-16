# EMSChatbot

Python FastAPI + LangGraph support chatbot for EMS. Calls the EMS REST API as
the logged-in user (JWT pass-through) and the Anthropic-compatible Presidio
gateway for the LLM.

## Setup
```bash
python -m venv .venv && . .venv/bin/activate
pip install -e ".[dev]"
cp .env.example .env   # fill in ANTHROPIC_AUTH_TOKEN and URLs
```

## Run
```bash
uvicorn app.main:app --reload --port 8100
```

## Test
```bash
pytest -q
```

## Verify gateway
```bash
python -m scripts.smoke_gateway
```
