from app.config import Settings

def test_settings_read_from_env(monkeypatch):
    monkeypatch.setenv("ANTHROPIC_BASE_URL", "https://gw.example.com")
    monkeypatch.setenv("ANTHROPIC_AUTH_TOKEN", "tok-123")
    monkeypatch.setenv("EMS_API_BASE_URL", "http://localhost:5222")
    s = Settings()
    assert s.anthropic_base_url == "https://gw.example.com"
    assert s.anthropic_auth_token == "tok-123"
    assert s.ems_api_base_url == "http://localhost:5222"
    assert s.model_name == "claude-sonnet-4-6"
