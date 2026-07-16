from __future__ import annotations
import httpx


class EmsAuthError(Exception):
    """EMS returned 401 — the user's token is missing/expired."""


class EmsApiError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(f"EMS error {status}: {message}")
        self.status = status
        self.message = message


# Central route table — matches the real EMS controllers: route template
# `api/v{version}/[controller]` with SINGULAR controller names (Event, Booking).
_ROUTES = {
    "search_events": "/api/v1/Event",
    "get_event": "/api/v1/Event/{event_id}",
    "my_bookings": "/api/v1/Booking/my",
    "get_booking": "/api/v1/Booking/{booking_id}",
    "cancel_pending": "/api/v1/Booking/{booking_id}/cancel",
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

    async def search_events(
        self,
        query: str = "",
        city: str = "",
        category: str = "",
        start_from: str = "",
        start_to: str = "",
    ):
        # Param names match EventSearchRequest (bound case-insensitively from the
        # query string). StartFrom/StartTo are IST wall-clock strings — the EMS
        # API converts inbound IST to UTC itself (TimeHelper.AssumeIstToUtc).
        params = {
            "query": query,
            "city": city,
            "category": category,
            "startFrom": start_from,
            "startTo": start_to,
        }
        params = {k: v for k, v in params.items() if v}
        return await self._request("GET", _ROUTES["search_events"], params=params)

    async def get_event(self, event_id: str):
        return await self._request("GET", _ROUTES["get_event"].format(event_id=event_id))

    async def get_my_bookings(self):
        return await self._request("GET", _ROUTES["my_bookings"])

    async def get_booking(self, booking_id: str):
        return await self._request("GET", _ROUTES["get_booking"].format(booking_id=booking_id))

    async def cancel_pending_booking(self, booking_id: str):
        return await self._request("POST", _ROUTES["cancel_pending"].format(booking_id=booking_id))


def _extract_message(resp: httpx.Response) -> str:
    try:
        data = resp.json()
        if isinstance(data, dict):
            return data.get("message") or data.get("error") or resp.text
        return resp.text
    except Exception:
        return resp.text or f"HTTP {resp.status_code}"
