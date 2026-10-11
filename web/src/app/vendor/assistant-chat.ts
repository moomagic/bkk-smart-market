import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, effect, inject } from '@angular/core';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { VendorStore } from './vendor-store';

/**
 * Tab ถามระเบียบ — Vendor Assistant as a full-screen chat: answers from the published rules only, with the file
 * and section it used. The question box stays at the bottom, above the tabs; the conversation lives in the store.
 */
@Component({
  selector: 'app-assistant-chat',
  imports: [Button, InputText],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="assistant-heading">
      <!-- The app bar names the screen; this names the region for assistive tech -->
      <h2 id="assistant-heading" class="sr-only">ถามผู้ช่วยเรื่องระเบียบ</h2>
      <p class="hint">ตอบจากระเบียบที่ประกาศเท่านั้น ไม่ตอบเรื่องผลของแต่ละคน</p>

      <!-- Always in the page, so screen readers announce the first exchange too (a new live region often is not) -->
      <ol class="messages" aria-live="polite">
        @for (m of messages(); track $index) {
          <li [class.me]="m.from === 'me'" [class.not-found]="m.found === false">
            {{ m.text }}
            @if (m.source) {
              <span class="source">{{ m.source === 'claude' ? 'ตอบโดย AI จากระเบียบที่ประกาศ' : 'ค้นจากหัวข้อระเบียบ' }}</span>
            }
          </li>
        }
        @if (busy()) {
          <li class="typing">กำลังตอบ…</li>
        }
      </ol>

      @if (messages().length === 0) {
        <!-- An empty chat offers questions to start from; once talking, they would only push the answers away -->
        <div class="start">
          <p>ถามเรื่องการยื่นคำขอ การจัดสรร หรือการยืนยันสิทธิ์ได้ เช่น</p>
          <div class="suggestions">
            @for (q of suggestions; track q) {
              <!-- Buttons, not p-chip: each suggestion is an action -->
              <p-button variant="outlined" severity="secondary" size="small" [label]="q" (onClick)="ask(q)" />
            }
          </div>
        </div>
      }

      <form (submit)="$event.preventDefault(); ask(draft(), true)">
        <label class="sr-only" for="assistant-question">คำถาม</label>
        <input
          pInputText
          id="assistant-question"
          type="text"
          maxlength="500"
          placeholder="พิมพ์คำถาม เช่น ต้องใช้เอกสารอะไร"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
        />
        <p-button type="submit" [label]="busy() ? 'กำลังตอบ…' : 'ถาม'" [disabled]="busy() || draft().trim().length === 0" />
      </form>
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100%; }
    section { flex: 1; display: flex; flex-direction: column; }
    .hint { color: var(--muted); font-size: var(--text-small); margin: 0 0 var(--space-3); }
    .start { flex: 1; }
    .start p { margin: 0 0 var(--space-3); }
    /* Empty, the live region stays in the page (and the accessibility tree) but takes no room */
    .messages:empty { flex: 0; }
    .messages { flex: 1; list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-2); }
    .messages li { white-space: pre-line; background: var(--accent-soft); border-radius: 12px 12px 12px 4px; padding: var(--space-2) var(--space-3); max-width: 88%; }
    .messages li.me { align-self: flex-end; background: var(--surface); border: 1px solid var(--line); border-radius: 12px 12px 4px 12px; }
    .messages li.not-found { background: var(--surface); border: 1px solid var(--line); color: var(--muted); }
    .messages li.typing { color: var(--muted); font-size: var(--text-small); background: transparent; padding-left: 0; }
    .source { display: block; margin-top: var(--space-1); font-size: var(--text-caption); color: var(--muted); }
    .suggestions { display: flex; flex-wrap: wrap; gap: var(--space-2); }
    /* The composer sticks to the bottom of the scrolling screen, edge to edge over its padding, just above the tabs */
    form {
      position: sticky;
      bottom: calc(-1 * var(--space-4));
      display: flex;
      gap: var(--space-2);
      margin: var(--space-4) calc(-1 * var(--space-4)) calc(-1 * var(--space-4));
      padding: var(--space-2) var(--space-4);
      background: var(--surface);
      border-top: 1px solid var(--line);
    }
    input { flex: 1; min-width: 0; }
  `,
})
export class AssistantChat {
  private readonly store = inject(VendorStore);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly suggestions = ['ต้องใช้เอกสารอะไร', 'จัดสรรล็อคยังไง', 'ต้องยืนยันสิทธิ์ภายในกี่วัน'];
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
}
