import json
import time

import jwt
import pytest
from unittest.mock import patch
from fastapi.testclient import TestClient
from langchain_core.messages import AIMessage

from tests.conftest import _ToolBindableFakeChatModel

SIGNING_KEY = "test-signing-key-at-least-32-bytes-long!!"
ISSUER = "EMSApi"
AUDIENCE = "EMSClient"


def _make_token(sub="42", **overrides):
    payload = {
        "sub": sub,
        "iss": ISSUER,
        "aud": AUDIENCE,
        "exp": int(time.time()) + 3600,
    }
    payload.update(overrides)
    return jwt.encode(payload, SIGNING_KEY, algorithm="HS256")


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setenv("ANTHROPIC_BASE_URL", "https://gw.test")
    monkeypatch.setenv("ANTHROPIC_AUTH_TOKEN", "tok")
    monkeypatch.setenv("EMS_API_BASE_URL", "http://ems.test")
    monkeypatch.setenv("JWT_SIGNING_KEY", SIGNING_KEY)
    monkeypatch.setenv("JWT_ISSUER", ISSUER)
    monkeypatch.setenv("JWT_AUDIENCE", AUDIENCE)
    from app import main
    main.get_settings.cache_clear()
    # NOTE: the brief uses a plain GenericFakeChatModel here, but under the
    # installed langgraph 1.2.9 / langchain-core 1.4.9, create_react_agent
    # always calls model.bind_tools(...), which raises NotImplementedError
    # on a raw GenericFakeChatModel. Reuse the tool-bindable fake from
    # conftest.py (same fix already applied for Task 5's test_agent.py).
    fake = _ToolBindableFakeChatModel(messages=iter([AIMessage(content="Hi there")]))
    with patch.object(main, "build_llm", return_value=fake) as build_llm_mock:
        yield TestClient(main.app), build_llm_mock


def test_missing_auth_returns_401(client):
    tc, build_llm_mock = client
    r = tc.post("/ai/chat", json={"message": "hi", "conversationId": "c1"})
    assert r.status_code == 401
    build_llm_mock.assert_not_called()


def test_invalid_token_returns_401_without_invoking_model(client):
    tc, build_llm_mock = client
    r = tc.post(
        "/ai/chat",
        headers={"Authorization": "Bearer not-a-real-jwt"},
        json={"message": "hi", "conversationId": "c1"},
    )
    assert r.status_code == 401
    build_llm_mock.assert_not_called()


def test_stream_emits_tokens_and_done(client):
    tc, _ = client
    r = tc.post(
        "/ai/chat",
        headers={"Authorization": f"Bearer {_make_token()}"},
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


def test_healthz_returns_ok(client):
    tc, _ = client
    for path in ("/healthz", "/ai/healthz"):
        r = tc.get(path)
        assert r.status_code == 200
        assert r.json() == {"status": "ok"}
