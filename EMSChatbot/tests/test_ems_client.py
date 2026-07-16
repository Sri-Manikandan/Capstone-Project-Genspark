import httpx
import pytest
import respx
from app.ems_client import EmsClient, EmsAuthError, EmsApiError

BASE = "http://ems.test"

@pytest.fixture
async def client():
    c = EmsClient(base_url=BASE, jwt="jwt-abc")
    yield c
    await c.aclose()

@respx.mock
async def test_get_my_bookings_sends_bearer_and_returns_json(client):
    route = respx.get(f"{BASE}/api/v1/bookings/me").mock(
        return_value=httpx.Response(200, json=[{"id": "b1"}])
    )
    result = await client.get_my_bookings()
    assert result == [{"id": "b1"}]
    assert route.calls.last.request.headers["Authorization"] == "Bearer jwt-abc"

@respx.mock
async def test_401_raises_auth_error(client):
    respx.get(f"{BASE}/api/v1/bookings/me").mock(return_value=httpx.Response(401))
    with pytest.raises(EmsAuthError):
        await client.get_my_bookings()

@respx.mock
async def test_400_raises_api_error_with_message(client):
    respx.post(f"{BASE}/api/v1/bookings/b1/cancel").mock(
        return_value=httpx.Response(400, json={"message": "Booking is not pending"})
    )
    with pytest.raises(EmsApiError) as exc:
        await client.cancel_pending_booking("b1")
    assert exc.value.status == 400
    assert "not pending" in exc.value.message
