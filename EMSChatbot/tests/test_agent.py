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


def test_current_ist_line_formats_ist_date():
    from datetime import datetime
    from zoneinfo import ZoneInfo
    from app.agent import current_ist_line

    fixed = datetime(2026, 7, 18, 14, 30, tzinfo=ZoneInfo("Asia/Kolkata"))
    assert current_ist_line(fixed) == "Today's date and time in IST: Saturday, 18 July 2026, 14:30."
