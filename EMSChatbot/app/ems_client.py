from __future__ import annotations
import httpx


class EmsAuthError(Exception):
    """EMS returned 401 — the user's token is missing/expired."""


class EmsApiError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(f"EMS error {status}: {message}")
        self.status = status
        self.message = message


# Central route table — confirm against real EMS controllers before use.
_ROUTES = {
    "search_events": "/api/v1/events",
    "get_event": "/api/v1/events/{event_id}",
    "my_bookings": "/api/v1/bookings/me",
    "get_booking": "/api/v1/bookings/{booking_id}",
    "resend_ticket": "/api/v1/bookings/{booking_id}/resend-ticket",
    "cancel_pending": "/api/v1/bookings/{booking_id}/cancel",
    "request_refund": "/api/v1/bookings/{booking_id}/refund",
}


class EmsClient:
    def __init__(self, base_url: str, jwt: str):
        self._http = httpx.AsyncClient(
            base_url=base_url,
            headers={"Authorization": f"Bearer {jwt}"},
            timeout=15.0,
        )

    async def aclose(self) -> None:
        await self._http.aclose()

    async def _request(self, method: str, path: str, **kwargs):
        resp = await self._http.request(method, path, **kwargs)
        if resp.status_code == 401:
            raise EmsAuthError()
        if resp.status_code >= 400:
            message = _extract_message(resp)
            raise EmsApiError(resp.status_code, message)
        if resp.content:
            return resp.json()
        return {}

    async def search_events(self, query: str):
        return await self._request("GET", _ROUTES["search_events"], params={"q": query})

    async def get_event(self, event_id: str):
        return await self._request("GET", _ROUTES["get_event"].format(event_id=event_id))

    async def get_my_bookings(self):
        return await self._request("GET", _ROUTES["my_bookings"])

    async def get_booking(self, booking_id: str):
        return await self._request("GET", _ROUTES["get_booking"].format(booking_id=booking_id))

    async def resend_ticket_email(self, booking_id: str):
        return await self._request("POST", _ROUTES["resend_ticket"].format(booking_id=booking_id))

    async def cancel_pending_booking(self, booking_id: str):
        return await self._request("POST", _ROUTES["cancel_pending"].format(booking_id=booking_id))

    async def request_refund(self, booking_id: str):
        return await self._request("POST", _ROUTES["request_refund"].format(booking_id=booking_id))


def _extract_message(resp: httpx.Response) -> str:
    try:
        data = resp.json()
        if isinstance(data, dict):
            return data.get("message") or data.get("error") or resp.text
        return resp.text
    except Exception:
        return resp.text or f"HTTP {resp.status_code}"
