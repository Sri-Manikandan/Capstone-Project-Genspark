from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    anthropic_base_url: str
    anthropic_auth_token: str
    ems_api_base_url: str
    model_name: str = "claude-sonnet-4-6"
    # Must match EMS appsettings Jwt:Key / Jwt:Issuer / Jwt:Audience so tokens
    # minted by the EMS AuthService validate here (HS256, symmetric key).
    jwt_signing_key: str
    jwt_issuer: str
    jwt_audience: str

@lru_cache
def get_settings() -> Settings:
    return Settings()
