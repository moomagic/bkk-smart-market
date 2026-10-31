const STORAGE_KEY = 'bkk-smart-market.device';

/** Used when the browser keeps nothing (private window, blocked storage): stable for this page load only. */
let sessionId: string | null = null;

/**
 * Simulated device identity sent with each check-in: a random id this browser keeps. The API stores only its hash,
 * to spot one device checking in for several winners (docs/rules › ข้อมูลอุปกรณ์ที่ใช้เช็คอิน).
 */
export function deviceId(): string {
  try {
    const kept = localStorage.getItem(STORAGE_KEY);
    if (kept) return kept;
    const id = crypto.randomUUID();
    localStorage.setItem(STORAGE_KEY, id);
    return id;
  } catch {
    return (sessionId ??= crypto.randomUUID());
  }
}
