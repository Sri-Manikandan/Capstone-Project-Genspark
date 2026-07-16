from unittest.mock import AsyncMock

from langgraph.checkpoint.memory import MemorySaver

from app.agent import build_agent, SYSTEM_PROMPT
from app.tools import build_tools


def test_system_prompt_scopes_to_ems_support():
    prompt = SYSTEM_PROMPT.lower()
    assert "booking" in prompt
    assert "decline unrelated" in prompt


async def test_agent_runs_and_returns_message(fake_llm):
    tools = build_tools(AsyncMock())
    agent = build_agent(fake_llm, tools, MemorySaver())
    result = await agent.ainvoke(
        {"messages": [("user", "hi")]},
        config={"configurable": {"thread_id": "t1"}},
    )
    assert result["messages"][-1].content == "Hello, how can I help?"
