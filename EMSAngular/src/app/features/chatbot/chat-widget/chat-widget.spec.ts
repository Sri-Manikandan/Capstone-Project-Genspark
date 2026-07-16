import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChatWidget } from './chat-widget';
import { ChatbotService } from '../../../core/services/chatbot.service';

describe('ChatWidget', () => {
  it('appends streamed tokens to an assistant message', () => {
    const svc = {
      stream: () => of(
        { type: 'token', text: 'Hel' },
        { type: 'token', text: 'lo' },
        { type: 'done' },
      ),
    };
    TestBed.configureTestingModule({
      imports: [ChatWidget],
      providers: [{ provide: ChatbotService, useValue: svc }],
    });
    const fixture = TestBed.createComponent(ChatWidget);
    const cmp = fixture.componentInstance;
    cmp.draft = 'hi';
    cmp.send();
    const assistant = cmp.messages.filter((m) => m.role === 'assistant').at(-1);
    expect(assistant?.text).toBe('Hello');
  });
});
