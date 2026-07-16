import pytest
from langchain_core.language_models.fake_chat_models import GenericFakeChatModel
from langchain_core.messages import AIMessage


class _ToolBindableFakeChatModel(GenericFakeChatModel):
    """GenericFakeChatModel with a working bind_tools passthrough.

    The installed langchain-core raises NotImplementedError from the base
    bind_tools, but create_react_agent always calls it when tools are
    supplied. This override is a no-op that keeps emitting the fixed
    message sequence unchanged.
    """

    def bind_tools(self, tools, *, tool_choice=None, **kwargs):
        return self


@pytest.fixture
def fake_llm():
    # Emits one fixed assistant message; enough to exercise graph wiring.
    return _ToolBindableFakeChatModel(messages=iter([AIMessage(content="Hello, how can I help?")]))
