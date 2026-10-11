import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, effect, inject } from '@angular/core';
import { Button } from 'primeng/button';
import { Textarea } from 'primeng/textarea';
import { ChatMessage, VendorStore } from './vendor-store';

/**
 * Tab ถามระเบียบ — Vendor Assistant as a full-screen chat: answers from the published rules only, with the file
 * and section it used. The question box stays at the bottom, above the tabs; the conversation lives in the store.
 */
@Component({
  selector: 'app-assistant-chat',
  imports: [Button, Textarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="assistant-heading">
      <!-- The app bar names the screen; this names the region for assistive tech -->
      <h2 id="assistant-heading" class="sr-only">ถามผู้ช่วยเรื่องระเบียบ</h2>

      @if (messages().length === 0) {
        <!-- An empty chat: one welcome, then questions to start from; once talking, both give way to the answers -->
        <div class="welcome">
          <span class="anchor" aria-hidden="true">
            <svg viewBox="0 0 24 24"><path d="M4 5h16v11H9l-5 4z" /><path d="M8 9.5h8M8 12.5h5" /></svg>
          </span>
          <p class="welcome-title">ถามเรื่องระเบียบการขอใช้พื้นที่ได้เลย</p>
          <p class="welcome-note">ตอบจากระเบียบที่ประกาศเท่านั้น ไม่ตอบเรื่องผลของแต่ละคน</p>
        </div>
        <ul class="suggestions" aria-label="ตัวอย่างคำถาม">
          @for (q of suggestions; track q) {
            <!-- Each row asks its question through the same flow as the box below -->
            <li>
              <button type="button" [disabled]="busy()" (click)="ask(q)">
                <span>{{ q }}</span>
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
              </button>
            </li>
          }
        </ul>
      }

      <!-- Always in the page, so screen readers announce the first exchange too (a new live region often is not) -->
      <ol class="messages" aria-live="polite">
        @for (m of messages(); track $index) {
          <li [class.me]="m.from === 'me'" [class.not-found]="m.found === false && !m.failed" [class.failed]="m.failed">
            @if (m.from === 'me') {
              <span class="sr-only">คุณถาม: </span>
            }
            <span class="body">{{ body(m) }}</span>
            @if (m.citations?.length) {
              <!-- The sections the answer used, as the assistant returned them -->
              <span class="refs">
                <!-- Trailing spaces keep the text a reader (or a copy) gets as "อ้างอิง: file › heading" -->
                <span class="refs-label">อ้างอิง: </span>
                @for (c of m.citations; track c.file + c.heading) {
                  <span class="ref">{{ c.file }} › {{ c.heading }} </span>
                }
              </span>
            }
            @if (m.source) {
              <span class="source">{{ m.source === 'claude' ? 'ตอบโดย AI จากระเบียบที่ประกาศ' : 'ค้นจากหัวข้อระเบียบ' }}</span>
            }
          </li>
        }
        @if (busy()) {
          <li class="typing">กำลังตอบ…</li>
        }
      </ol>

      <form (submit)="$event.preventDefault(); send()">
        <label class="sr-only" for="assistant-question">คำถาม</label>
        <!-- Grows with a longer question up to four lines; Enter sends, Shift+Enter starts a new line -->
        <textarea
          pTextarea
          id="assistant-question"
          rows="1"
          autoResize
          maxlength="500"
          placeholder="พิมพ์คำถาม เช่น ต้องใช้เอกสารอะไร"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
          (keydown.enter)="onEnter($any($event))"
        ></textarea>
        <p-button type="submit" [label]="busy() ? 'กำลังตอบ…' : 'ถาม'" [disabled]="busy() || draft().trim().length === 0" />
      </form>
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100%; }
    section { flex: 1; display: flex; flex-direction: column; }

    /* Welcome: an icon on its soft tint, one line that invites, one that sets the limits */
    .welcome { padding: var(--space-4) 0 var(--space-5); }
    .anchor { display: grid; place-items: center; width: 48px; height: 48px; margin-bottom: var(--space-3); border-radius: 50%; background: var(--accent-soft); color: var(--accent); }
    .anchor svg { width: 24px; height: 24px; }
    .welcome p { margin: 0; }
    .welcome-title { font-family: var(--font-heading); font-size: var(--text-h3); font-weight: 600; line-height: 1.35; }
    .welcome .welcome-note { margin-top: var(--space-1); color: var(--muted); font-size: var(--text-small); }

    /* Suggested questions: one bordered list, rows divided, each row a 48px target */
    /* margin-bottom: auto keeps the composer at the foot of an empty chat, not right under the rows */
    .suggestions { list-style: none; margin: 0 0 auto; padding: 0; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; }
    .suggestions li + li { border-top: 1px solid var(--line); }
    .suggestions button {
      display: flex; align-items: center; justify-content: space-between; gap: var(--space-3);
      width: 100%; min-height: 48px; padding: var(--space-2) var(--space-4);
      font: inherit; text-align: left; color: var(--ink); background: transparent; border: 0; cursor: pointer;
      -webkit-tap-highlight-color: transparent;
    }
    .suggestions li:first-child button { border-radius: 8px 8px 0 0; }
    .suggestions li:last-child button { border-radius: 0 0 8px 8px; }
    .suggestions button:active { background: var(--accent-soft); }
    .suggestions button:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }
    .suggestions button:disabled { color: var(--muted); cursor: default; }
    .suggestions button svg { flex-shrink: 0; color: var(--accent); }

    svg { width: 20px; height: 20px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }

    /* Empty, the live region stays in the page (and the accessibility tree) but takes no room */
    .messages:empty { flex: 0; }
    .messages { flex: 1; list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-3); }
    .messages li { display: flex; flex-direction: column; max-width: 88%; padding: var(--space-3); background: var(--accent-soft); border-radius: 8px 8px 8px 2px; overflow-wrap: anywhere; }
    .messages li.me { align-self: flex-end; background: var(--surface); border: 1px solid var(--line); border-radius: 8px 8px 2px 8px; padding-block: var(--space-2); }
    .messages li.not-found { background: var(--surface); border: 1px solid var(--line); color: var(--muted); }
    .messages li.failed { background: var(--surface); border: 1px solid var(--warn); color: var(--warn); }
    .messages li.typing { color: var(--muted); font-size: var(--text-small); background: transparent; padding: 0; }
    /* Answers come back with line breaks: keep them */
    .body { white-space: pre-line; }
    /* References sit under a hairline in the bubble, one section per line */
    .refs { display: flex; flex-direction: column; margin-top: var(--space-2); padding-top: var(--space-2); border-top: 1px solid color-mix(in srgb, var(--accent) 22%, transparent); font-size: var(--text-small); }
    .refs-label { font-weight: 600; color: var(--accent); }
    .source { margin-top: var(--space-1); font-size: var(--text-small); color: var(--muted); }

    /* The composer sticks to the bottom of the scrolling screen, edge to edge over its padding, just above the tabs */
    form {
      position: sticky;
      bottom: calc(-1 * var(--space-4));
      display: flex;
      align-items: flex-end;
      gap: var(--space-2);
      margin: var(--space-4) calc(-1 * var(--space-4)) calc(-1 * var(--space-4));
      padding: var(--space-2) var(--space-4);
      background: var(--surface);
      border-top: 1px solid var(--line);
    }
    /* autoResize writes overflow: hidden inline; past four lines the box scrolls instead of hiding the rest */
    textarea { flex: 1; min-width: 0; min-height: 44px; max-height: calc(4 * 1.6em + 18px); resize: none; overflow-y: auto !important; }
  `,
})
export class AssistantChat {
  private readonly store = inject(VendorStore);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly suggestions = ['ต้องใช้เอกสารอะไร', 'จัดสรรล็อคยังไง', 'ต้องยืนยันสิทธิ์ภายในกี่วัน', 'ต้องเช็คอินทุกวันไหม'];
  protected readonly messages = this.store.assistant.messages;
  protected readonly draft = this.store.assistant.draft;
  protected readonly busy = this.store.assistant.busy;

  constructor() {
    let first = true;
    effect(() => {
      const messages = this.messages();
      const scroller = this.store.scroller();
      if (!scroller) return;
      // Follow the conversation, but leave a vendor who scrolled up to reread an answer where they are:
      // always on arriving at the tab and after their own question, otherwise only if they were at the end
      const last = messages.at(-1);
      const atEnd = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 120;
      const arriving = first;
      first = false;
      if (!arriving && !atEnd && last?.from !== 'me') return;
      afterNextRender(
        () => {
          const answer = this.host.nativeElement.querySelector<HTMLElement>('.messages > li:last-child');
          if (!arriving && last?.from === 'assistant' && answer) {
            // A new answer opens at its first line; a long one would otherwise land with only its end in view
            const top = answer.getBoundingClientRect().top - scroller.getBoundingClientRect().top;
            scroller.scrollTo({ top: scroller.scrollTop + top - 16 });
          } else {
            scroller.scrollTo({ top: scroller.scrollHeight });
          }
        },
        { injector: this.injector },
      );
    });
  }

  protected ask(question: string, fromDraft = false): void {
    this.store.ask(question, fromDraft);
  }

  protected send(): void {
    this.ask(this.draft(), true);
  }

  /** Enter sends; Shift+Enter, or Enter that ends a Thai IME composition, stays in the box. */
  protected onEnter(e: KeyboardEvent): void {
    if (e.shiftKey || e.isComposing) return;
    e.preventDefault();
    this.send();
  }

  /** The answer without its trailing "อ้างอิง: …" line when the citations are shown on their own below it. */
  protected body(m: ChatMessage): string {
    if (!m.citations?.length) return m.text;
    const lines = m.text.split('\n');
    return lines.at(-1)?.startsWith('อ้างอิง') ? lines.slice(0, -1).join('\n') : m.text;
  }
}
