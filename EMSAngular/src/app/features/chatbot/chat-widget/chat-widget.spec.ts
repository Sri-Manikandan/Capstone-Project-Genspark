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

describe('ChatWidget markdown', () => {
  function widget() {
    TestBed.configureTestingModule({
      imports: [ChatWidget],
      providers: [{ provide: ChatbotService, useValue: { stream: () => of() } }],
    });
    return TestBed.createComponent(ChatWidget).componentInstance;
  }

  it('renders bold, bullets, and line breaks', () => {
    const html = widget().renderMarkdown('**Leo** show\n- Seat A1\n- Seat A2');
    expect(html).toContain('<strong>Leo</strong>');
    expect(html).toContain('<span class="li">Seat A1</span>');
    expect(html).toContain('<br>');
  });

  it('escapes HTML so model output cannot inject markup', () => {
    const html = widget().renderMarkdown('<img src=x onerror=alert(1)> & **bold**');
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;img');
    expect(html).toContain('&amp;');
    expect(html).toContain('<strong>bold</strong>');
  });

  it('sends a suggestion chip as a message', () => {
    const cmp = widget();
    cmp.ask('Show my bookings');
    expect(cmp.messages[0]).toEqual({ role: 'user', text: 'Show my bookings' });
  });
});
