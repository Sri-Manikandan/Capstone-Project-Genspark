import json
import pytest
from unittest.mock import patch
from fastapi.testclient import TestClient
from langchain_core.messages import AIMessage

from tests.conftest import _ToolBindableFakeChatModel


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setenv("ANTHROPIC_BASE_URL", "https://gw.test")
    monkeypatch.setenv("ANTHROPIC_AUTH_TOKEN", "tok")
    monkeypatch.setenv("EMS_API_BASE_URL", "http://ems.test")
    from app import main
    main.get_settings.cache_clear()
    # NOTE: the brief uses a plain GenericFakeChatModel here, but under the
    # installed langgraph 1.2.9 / langchain-core 1.4.9, create_react_agent
    # always calls model.bind_tools(...), which raises NotImplementedError
    # on a raw GenericFakeChatModel. Reuse the tool-bindable fake from
    # conftest.py (same fix already applied for Task 5's test_agent.py).
    fake = _ToolBindableFakeChatModel(messages=iter([AIMessage(content="Hi there")]))
    with patch.object(main, "build_llm", return_value=fake):
        yield TestClient(main.app)


def test_missing_auth_returns_401(client):
    r = client.post("/ai/chat", json={"message": "hi", "conversationId": "c1"})
    assert r.status_code == 401


def test_stream_emits_tokens_and_done(client):
    r = client.post(
        "/ai/chat",
        headers={"Authorization": "Bearer jwt-1"},
        json={"message": "hi", "conversationId": "c1"},
    )
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")
    payloads = [
        json.loads(line[len("data: "):])
        for line in r.text.splitlines()
        if line.startswith("data: ")
    ]
    types = [p["type"] for p in payloads]
    assert "token" in types
    assert types[-1] == "done"
    text = "".join(p["text"] for p in payloads if p["type"] == "token")
    assert "Hi there" in text
