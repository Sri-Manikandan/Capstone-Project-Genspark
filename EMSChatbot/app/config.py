from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    anthropic_base_url: str
    anthropic_auth_token: str
    ems_api_base_url: str
    model_name: str = "claude-sonnet-4-6"

@lru_cache
def get_settings() -> Settings:
    return Settings()
