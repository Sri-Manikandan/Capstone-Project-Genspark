from __future__ import annotations
import jwt
from fastapi import HTTPException


def validate_token(token: str, settings) -> dict:
    """Validate an EMS-issued JWT locally and return its claims.

    Matches EMS's AuthService signing: HS256 with the symmetric `Jwt:Key`, and
    the `Jwt:Issuer` / `Jwt:Audience` values. Signature, issuer, audience and
    expiry are all verified. Any failure raises HTTPException(401) so the LLM
    gateway is never invoked for an unauthenticated caller.

    The user id lives in the `sub` claim (EMS sets JwtRegisteredClaimNames.Sub
    to user.Id in GenerateAccessToken).
    """
    try:
        claims = jwt.decode(
            token,
            settings.jwt_signing_key,
            algorithms=["HS256"],
            issuer=settings.jwt_issuer,
            audience=settings.jwt_audience,
        )
    except jwt.PyJWTError:
        raise HTTPException(status_code=401, detail="Invalid or expired token")

    if not claims.get("sub"):
        raise HTTPException(status_code=401, detail="Token missing subject")
    return claims
