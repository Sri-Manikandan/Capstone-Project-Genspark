from __future__ import annotations
import json
from langchain_core.tools import StructuredTool, BaseTool
from app.ems_client import EmsClient, EmsApiError


def _dump(value) -> str:
    return json.dumps(value, default=str)


def build_tools(ems: EmsClient) -> list[BaseTool]:
    async def search_events(query: str) -> str:
        """Search events by keyword. Returns matching events as JSON."""
        return _dump(await ems.search_events(query))

    async def get_event_details(event_id: str) -> str:
        """Get full details for one event by its id."""
        return _dump(await ems.get_event(event_id))

    async def get_my_bookings() -> str:
        """List the current user's bookings. Takes no arguments."""
        return _dump(await ems.get_my_bookings())

    async def get_booking_details(booking_id: str) -> str:
        """Get details for one of the current user's bookings by id."""
        return _dump(await ems.get_booking(booking_id))

    async def resend_ticket_email(booking_id: str) -> str:
        """Re-send the ticket/confirmation email for one of the user's bookings."""
        try:
            await ems.resend_ticket_email(booking_id)
            return "The ticket email has been re-sent."
        except EmsApiError as e:
            return f"I couldn't resend the ticket: {e.message}"

    async def cancel_pending_booking(booking_id: str) -> str:
        """Cancel a PENDING (unpaid) booking. Fails if it is already paid."""
        try:
            await ems.cancel_pending_booking(booking_id)
            return "The pending booking has been cancelled."
        except EmsApiError as e:
            return f"I couldn't cancel that booking: {e.message}"

    async def request_refund(booking_id: str) -> str:
        """Request a refund for an eligible paid booking. Only call AFTER the
        user has explicitly confirmed they want the refund."""
        try:
            await ems.request_refund(booking_id)
            return "Your refund request has been submitted."
        except EmsApiError as e:
            return f"I couldn't request a refund: {e.message}"

    def _mk(fn, name):
        return StructuredTool.from_function(coroutine=fn, name=name, description=fn.__doc__)

    return [
        _mk(search_events, "search_events"),
        _mk(get_event_details, "get_event_details"),
        _mk(get_my_bookings, "get_my_bookings"),
        _mk(get_booking_details, "get_booking_details"),
        _mk(resend_ticket_email, "resend_ticket_email"),
        _mk(cancel_pending_booking, "cancel_pending_booking"),
        _mk(request_refund, "request_refund"),
    ]
