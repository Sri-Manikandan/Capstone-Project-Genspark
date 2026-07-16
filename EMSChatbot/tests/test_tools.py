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
        "get_booking_details", "cancel_pending_booking",
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


async def test_search_tool_passes_filters_to_client():
    ems = AsyncMock()
    ems.search_events.return_value = []
    tools = build_tools(ems)
    await _tool(tools, "search_events").ainvoke({
        "city": "Chennai",
        "start_from": "2026-07-18T00:00",
        "start_to": "2026-07-19T23:59",
    })
    ems.search_events.assert_awaited_once_with(
        query="", city="Chennai", category="",
        start_from="2026-07-18T00:00", start_to="2026-07-19T23:59",
    )
