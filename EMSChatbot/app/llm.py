from langchain_anthropic import ChatAnthropic

def build_llm(settings):
    # Gateway is Anthropic-compatible and authenticates by bearer token.
    # api_key is required by the constructor but unused by the gateway; the real
    # credential is the Authorization header below.
    return ChatAnthropic(
        model=settings.model_name,
        base_url=settings.anthropic_base_url,
        api_key="gateway-bearer-auth",
        default_headers={"Authorization": f"Bearer {settings.anthropic_auth_token}"},
        streaming=True,
        max_tokens=1024,
    )

# FALLBACK (if the gateway rejects the above because both x-api-key and
# Authorization are sent): pass the token as api_key instead and drop
# default_headers —
#   return ChatAnthropic(model=settings.model_name,
#                        base_url=settings.anthropic_base_url,
#                        api_key=settings.anthropic_auth_token,
#                        streaming=True, max_tokens=1024)
