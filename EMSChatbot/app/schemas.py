from pydantic import BaseModel, Field

class ChatRequest(BaseModel):
    message: str
    conversation_id: str = Field(alias="conversationId")

    model_config = {"populate_by_name": True}
