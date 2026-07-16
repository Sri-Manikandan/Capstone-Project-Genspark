import pytest
from unittest.mock import AsyncMock
from app.tools import build_tools
from app.ems_client import EmsApiError


def _tool(tools, name):
    return next(t for t in tools if t.name == name)


def test_build_tools_exposes_expected_names():
    ems = AsyncMock()
    names = {t.name for t in build_tools(ems)}
    assert names == {
        "search_events", "get_event_details", "get_my_bookings",
        "get_booking_details", "resend_ticket_email",
        "cancel_pending_booking", "request_refund",
    }


async def test_get_my_bookings_tool_calls_client():
    ems = AsyncMock()
    ems.get_my_bookings.return_value = [{"id": "b1"}]
    tools = build_tools(ems)
    out = await _tool(tools, "get_my_bookings").ainvoke({})
    ems.get_my_bookings.assert_awaited_once()
    assert "b1" in out


async def test_cancel_tool_relays_api_error_as_text():
    ems = AsyncMock()
    ems.cancel_pending_booking.side_effect = EmsApiError(400, "Booking is not pending")
    tools = build_tools(ems)
    out = await _tool(tools, "cancel_pending_booking").ainvoke({"booking_id": "b1"})
    assert "not pending" in out.lower()
