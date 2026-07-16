import asyncio
from app.config import get_settings
from app.llm import build_llm

async def main():
    llm = build_llm(get_settings())
    resp = await llm.ainvoke("Reply with exactly: OK")
    print("MODEL REPLY:", resp.content)

if __name__ == "__main__":
    asyncio.run(main())
