import { TestBed } from '@angular/core/testing';
import { ChatbotService, ChatEvent } from './chatbot.service';
import { AuthService } from './auth.service';

describe('ChatbotService', () => {
  let service: ChatbotService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ChatbotService,
        { provide: AuthService, useValue: { accessToken: () => 'jwt-xyz' } },
      ],
    });
    service = TestBed.inject(ChatbotService);
  });

  it('parses SSE lines into ChatEvents', async () => {
    const body = 'data: {"type":"token","text":"Hi"}\n\ndata: {"type":"done"}\n\n';
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(body, { headers: { 'Content-Type': 'text/event-stream' } }),
    );
    const seen: ChatEvent[] = [];
    await new Promise<void>((resolve) => {
      service.stream('hello', 'c1').subscribe({
        next: (e) => seen.push(e),
        complete: resolve,
      });
    });
    expect(seen[0]).toEqual({ type: 'token', text: 'Hi' });
    expect(seen.at(-1)).toEqual({ type: 'done' });
  });
});
