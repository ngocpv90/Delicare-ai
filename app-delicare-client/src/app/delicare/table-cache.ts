/**
 * Bộ nhớ đệm dữ liệu bảng của engine (IndexedDB) – "vào là có dữ liệu":
 * lần mở sau engine vẽ ngay bằng dữ liệu đã lưu, đồng thời tải bản mới từ API ở nền rồi vẽ lại (core/store.js).
 * - Tách theo tài khoản + phiên bản build: đổi tài khoản / deploy bản mới → xóa sạch, tải lại từ đầu.
 * - Xóa khi đăng xuất / hết phiên (AuthenticationService.clearSession) – máy dùng chung không để lại dữ liệu.
 * - Dữ liệu cũ hơn MAX_AGE_MS không dùng. IndexedDB lỗi / chậm (trình duyệt chặn) → chạy như cũ, không đệm.
 */
const DB_NAME = 'dc-table-cache';
const TABLES = 'tables';
const META = 'meta';
const MAX_AGE_MS = 24 * 60 * 60 * 1000;
const OPEN_TIMEOUT_MS = 800;
/** Bảng quá lớn thì không lưu (tránh ghi nặng mỗi lần tải). */
const MAX_ROWS = 20000;

export interface TableCache {
  /** Dữ liệu đã lưu theo tên bảng (còn hạn, đúng tài khoản + phiên bản). */
  rows: Record<string, any[]>;
  put(table: string, rows: any[]): void;
}

const EMPTY: TableCache = { rows: {}, put: () => undefined };

const req = <T>(r: IDBRequest<T>): Promise<T> =>
  new Promise((resolve, reject) => {
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
  });

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(DB_NAME, 1);
    r.onupgradeneeded = () => {
      const db = r.result;
      if (!db.objectStoreNames.contains(TABLES)) db.createObjectStore(TABLES);
      if (!db.objectStoreNames.contains(META)) db.createObjectStore(META);
    };
    r.onsuccess = () => {
      // Đăng xuất / hết phiên gọi deleteDatabase khi trang vẫn mở → đóng kết nối để lệnh xóa chạy được ngay.
      r.result.onversionchange = () => r.result.close();
      resolve(r.result);
    };
    r.onerror = () => reject(r.error);
    r.onblocked = () => reject(new Error('blocked'));
  });
}

async function load(owner: string): Promise<TableCache> {
  const db = await openDb();
  const meta = await req(db.transaction(META).objectStore(META).get('owner'));
  if (meta !== owner) {
    const tx = db.transaction([TABLES, META], 'readwrite');
    tx.objectStore(TABLES).clear();
    tx.objectStore(META).put(owner, 'owner');
  }
  const rows: Record<string, any[]> = {};
  if (meta === owner) {
    const store = db.transaction(TABLES).objectStore(TABLES);
    const [keys, values] = await Promise.all([req(store.getAllKeys()), req(store.getAll())]);
    const now = Date.now();
    keys.forEach((k, i) => {
      const v = values[i];
      if (v && Array.isArray(v.rows) && now - (v.ts || 0) < MAX_AGE_MS) rows[String(k)] = v.rows;
    });
  }
  return {
    rows,
    put: (table, data) => {
      if (!Array.isArray(data) || data.length > MAX_ROWS) return;
      try {
        db.transaction(TABLES, 'readwrite').objectStore(TABLES).put({ ts: Date.now(), rows: data }, table);
      } catch {
        /* hết dung lượng / DB đã đóng: bỏ qua, lần sau tải từ API */
      }
    },
  };
}

/** Mở bộ đệm cho tài khoản + phiên bản build; không bao giờ lỗi, tối đa OPEN_TIMEOUT_MS. */
export function openTableCache(userKey: string, version: string): Promise<TableCache> {
  if (!userKey || typeof indexedDB === 'undefined') return Promise.resolve(EMPTY);
  const timeout = new Promise<TableCache>((resolve) => setTimeout(() => resolve(EMPTY), OPEN_TIMEOUT_MS));
  return Promise.race([load(userKey + '|' + version).catch(() => EMPTY), timeout]);
}

/** Xóa toàn bộ bộ đệm (đăng xuất / hết phiên). */
export function clearTableCache(): void {
  try {
    if (typeof indexedDB !== 'undefined') indexedDB.deleteDatabase(DB_NAME);
  } catch {
    /* bỏ qua */
  }
}
