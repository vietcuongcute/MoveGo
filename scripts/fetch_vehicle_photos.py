#!/usr/bin/env python3
"""
MoveGo · Tìm ảnh xe THẬT trên Wikimedia Commons (chỉ ảnh có giấy phép tự do) rồi gắn vào các xe mẫu.

Chạy 2 bước để bạn kiểm tra ảnh trước khi dùng:

  1) python3 scripts/fetch_vehicle_photos.py fetch
        → tìm + tải ảnh vào thư mục photo_staging/<xe>/ và tạo photo_staging/preview.html
        → MỞ preview.html bằng trình duyệt, xoá file ảnh nào sai xe / xấu (xoá thẳng file .jpg trong thư mục).

  2) python3 scripts/fetch_vehicle_photos.py apply --key movego-admin-dev
        → copy ảnh còn lại vào MoveGo.Web/wwwroot/uploads/vehicles/, cập nhật bộ ảnh của từng xe qua API admin,
          và ghi file ghi nguồn ảnh (hiện ở trang /nguon-anh của website).

Chỉ dùng thư viện có sẵn của Python 3 (không cần pip install). Cần API đang chạy khi chạy bước `apply`.
Ảnh Wikimedia dùng giấy phép CC BY / CC BY-SA / CC0 / public domain: BẮT BUỘC ghi nguồn → đừng xoá trang /nguon-anh.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STAGING = ROOT / "photo_staging"
UPLOAD_DIR = ROOT / "MoveGo.Web" / "wwwroot" / "uploads" / "vehicles"
UPLOAD_URL = "/uploads/vehicles"
CREDITS_FILE = UPLOAD_DIR / "credits.json"
USER_AGENT = "MoveGoPhotoFetcher/1.0 (https://github.com/vietcuongcute/movego; student project)"

# Tên xe PHẢI trùng với tên trong database (xem SeedData.cs). `must` là regex bắt buộc khớp với
# tên file + mô tả + danh mục của ảnh trên Commons để chắc chắn đúng dòng xe.
VEHICLES = [
    dict(name="VinFast VF 3", slug="vf3",
         categories=["VinFast VF 3"], queries=["VinFast VF 3", "VinFast VF3"],
         must=r"(?<![A-Za-z])VF[ _-]?3(?![0-9])"),
    dict(name="VinFast VF 5 Plus", slug="vf5",
         categories=["VinFast VF 5", "VinFast VF 5 Plus"], queries=["VinFast VF 5", "VinFast VF5 Plus"],
         must=r"(?<![A-Za-z])VF[ _-]?5(?![0-9])"),
    dict(name="VinFast VF 6", slug="vf6",
         categories=["VinFast VF 6"], queries=["VinFast VF 6", "VinFast VF6"],
         must=r"(?<![A-Za-z])VF[ _-]?6(?![0-9])"),
    dict(name="VinFast VF 8", slug="vf8",
         categories=["VinFast VF 8"], queries=["VinFast VF 8", "VinFast VF8"],
         must=r"(?<![A-Za-z])VF[ _-]?8(?![0-9])"),
    dict(name="VinFast VF 9", slug="vf9",
         categories=["VinFast VF 9"], queries=["VinFast VF 9", "VinFast VF9"],
         must=r"(?<![A-Za-z])VF[ _-]?9(?![0-9])"),
    dict(name="VinFast VF 7", slug="vf7",
         categories=["VinFast VF 7"], queries=["VinFast VF 7", "VinFast VF7"],
         must=r"(?<![A-Za-z])VF[ _-]?7(?![0-9])"),
    dict(name="Limo Green 7 chỗ", slug="limogreen",
         categories=["VinFast Limo Green"], queries=["VinFast Limo Green", "Limo Green VinFast"],
         must=r"Limo[ _-]?Green"),
    dict(name="VinFast Evo 200", slug="evo200",
         categories=["VinFast Evo200", "VinFast Evo 200", "VinFast Evo"], queries=["VinFast Evo200", "VinFast Evo 200"],
         must=r"Evo[ _-]?200"),
    dict(name="VinFast Feliz S", slug="felizs",
         categories=["VinFast Feliz S", "VinFast Feliz"], queries=["VinFast Feliz S", "VinFast Feliz"],
         must=r"Feliz"),
    dict(name="VinFast Klara S", slug="klaras",
         categories=["VinFast Klara S", "VinFast Klara"], queries=["VinFast Klara S", "VinFast Klara"],
         must=r"Klara"),
]

BAD_WORDS = re.compile(r"(?i)\b(sketch|render|drawing|logo|diagram|map|concept|scale[ _]model|toy|lego|screenshot|poster|advert)\b")
INTERIOR_WORDS = re.compile(r"(?i)(interior|dashboard|cockpit|seat|steering|trunk|boot|charging|charger|badge|emblem|wheel|engine|"
                            r"nội thất|taplo|cốp|sạc)")
OK_LICENSE = re.compile(r"(?i)^(cc[ -]?by(?![\w-]*(nc|nd))[\w .-]*|cc0[\w .-]*|public domain.*|pd[ -].*)$")


# ----------------------------------------------------------------------------------------------
# HTTP
# ----------------------------------------------------------------------------------------------
def http(url: str, *, method="GET", headers=None, data: bytes | None = None, retries=4, binary=False):
    h = {"User-Agent": USER_AGENT, **(headers or {})}
    for attempt in range(retries):
        req = urllib.request.Request(url, data=data, headers=h, method=method)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                body = r.read()
                return body if binary else body.decode("utf-8")
        except urllib.error.HTTPError as e:
            if e.code in (429, 503) and attempt < retries - 1:
                wait = int(e.headers.get("Retry-After", "0") or 0) or 3 * (attempt + 1)
                print(f"   … server bận ({e.code}), chờ {wait}s rồi thử lại")
                time.sleep(wait)
                continue
            raise
        except urllib.error.URLError as e:
            if "CERTIFICATE_VERIFY_FAILED" in str(e):
                sys.exit("Lỗi chứng chỉ SSL của Python trên Mac. Chạy file 'Install Certificates.command' trong thư mục "
                         "/Applications/Python 3.x/ rồi chạy lại script (đừng tắt kiểm tra SSL).")
            if attempt < retries - 1:
                time.sleep(2 * (attempt + 1))
                continue
            raise
    raise RuntimeError("unreachable")


def commons(base: str, **params):
    params.setdefault("format", "json")
    return json.loads(http(f"{base}?{urllib.parse.urlencode(params)}"))


# ----------------------------------------------------------------------------------------------
# Tìm ảnh
# ----------------------------------------------------------------------------------------------
def category_files(base: str, cat: str, depth=1) -> list[str]:
    titles: list[str] = []
    subcats: list[str] = []
    cont: dict = {}
    while True:
        data = commons(base, action="query", list="categorymembers", cmtitle=f"Category:{cat}",
                       cmtype="file|subcat", cmlimit="100", **cont)
        for m in data.get("query", {}).get("categorymembers", []):
            (titles if m["ns"] == 6 else subcats).append(m["title"])
        if "continue" in data and len(titles) < 300:
            cont = data["continue"]
        else:
            break
    if depth > 0:
        for sc in subcats[:8]:
            titles += category_files(base, sc.removeprefix("Category:"), depth - 1)
    return titles


def search_files(base: str, q: str) -> list[str]:
    data = commons(base, action="query", list="search", srsearch=q, srnamespace="6", srlimit="40")
    return [m["title"] for m in data.get("query", {}).get("search", [])]


def image_infos(base: str, titles: list[str], width: int) -> list[dict]:
    out = []
    for i in range(0, len(titles), 40):
        chunk = titles[i:i + 40]
        data = commons(base, action="query", titles="|".join(chunk), prop="imageinfo",
                       iiprop="url|size|mime|extmetadata", iiurlwidth=str(width))
        for page in data.get("query", {}).get("pages", {}).values():
            info = (page.get("imageinfo") or [None])[0]
            if info:
                out.append({"title": page["title"], **info})
    return out


def meta(info: dict, key: str) -> str:
    v = (info.get("extmetadata") or {}).get(key, {}).get("value", "")
    return html.unescape(re.sub(r"<[^>]+>", "", v)).strip()


def pick(base: str, veh: dict, limit: int, width: int) -> list[dict]:
    titles: list[str] = []
    for c in veh["categories"]:
        try:
            titles += category_files(base, c)
        except urllib.error.HTTPError:
            pass
    for q in veh["queries"]:
        titles += search_files(base, q)
    titles = list(dict.fromkeys(t for t in titles if re.search(r"(?i)\.jpe?g$", t)))
    print(f"   {len(titles)} ảnh ứng viên")

    must = re.compile(veh["must"], re.I)
    good = []
    for info in image_infos(base, titles, width):
        if info.get("mime") != "image/jpeg" or info.get("width", 0) < 900 or info.get("height", 0) < 500:
            continue
        license_name = meta(info, "LicenseShortName")
        if not OK_LICENSE.match(license_name):
            continue
        text = " ".join([info["title"], meta(info, "ObjectName"), meta(info, "ImageDescription"),
                         meta(info, "Categories").replace("|", " ")])
        if not must.search(text) or BAD_WORDS.search(text):
            continue
        author = meta(info, "Artist") or "Không rõ tác giả"
        good.append({
            "title": info["title"],
            "page": info.get("descriptionurl", ""),
            "url": info.get("thumburl") or info["url"],
            "width": info["width"],
            "author": author[:160],
            "license": license_name,
            "licenseUrl": meta(info, "LicenseUrl"),
            "interior": bool(INTERIOR_WORDS.search(info["title"] + " " + meta(info, "ObjectName"))),
        })
    # ảnh ngoại thất trước (làm ảnh bìa), ảnh nét hơn trước
    good.sort(key=lambda g: (g["interior"], -g["width"]))
    return good[:limit]


def cmd_fetch(a):
    base = a.commons
    STAGING.mkdir(exist_ok=True)
    sections = []
    for veh in VEHICLES:
        print(f"▶ {veh['name']}")
        folder = STAGING / veh["slug"]
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir(parents=True)
        credits = []
        for n, g in enumerate(pick(base, veh, a.max, a.width), 1):
            time.sleep(0.6)
            try:
                data = http(g["url"], binary=True)
            except urllib.error.HTTPError as e:
                print(f"   ✗ bỏ qua {g['title']} ({e.code})")
                continue
            if data[:3] != b"\xff\xd8\xff" or len(data) > 4_800_000:
                print(f"   ✗ bỏ qua {g['title']} (không phải JPG hợp lệ hoặc quá nặng)")
                continue
            fname = f"{n:02d}.jpg"
            (folder / fname).write_bytes(data)
            credits.append({**g, "file": fname, "sha1": hashlib.sha1(data).hexdigest()})
            print(f"   ✓ {fname}  {g['title']}  [{g['license']}]")
        (folder / "credits.json").write_text(json.dumps(credits, ensure_ascii=False, indent=2), encoding="utf-8")
        sections.append((veh, credits))
        if not credits:
            print("   ⚠ Không tìm được ảnh phù hợp cho xe này — xe sẽ giữ nguyên hình hiện tại.")

    write_preview(sections)
    print(f"\nXong. Mở {STAGING / 'preview.html'} để duyệt ảnh, xoá file nào sai, rồi chạy bước `apply`.")


def write_preview(sections):
    parts = ["<!doctype html><meta charset=utf-8><title>Duyệt ảnh xe MoveGo</title>"
             "<style>body{font:15px system-ui;margin:24px;max-width:1200px}h2{margin-top:40px}"
             ".g{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:14px}"
             "figure{margin:0;border:1px solid #ddd;border-radius:10px;overflow:hidden}"
             "img{width:100%;aspect-ratio:4/3;object-fit:cover;display:block}"
             "figcaption{padding:8px 10px;font-size:12px;color:#444}b{color:#000}</style>"
             "<h1>Duyệt ảnh xe</h1><p>Xoá file ảnh sai xe/xấu trong thư mục <code>photo_staging/&lt;xe&gt;/</code>, rồi chạy <code>apply</code>. "
             "Ảnh đầu tiên (01) sẽ là ảnh bìa.</p>"]
    for veh, credits in sections:
        parts.append(f"<h2>{html.escape(veh['name'])} <small>({len(credits)} ảnh)</small></h2><div class=g>")
        for c in credits:
            parts.append(f"<figure><img src='{veh['slug']}/{c['file']}'><figcaption><b>{veh['slug']}/{c['file']}</b><br>"
                         f"<a href='{html.escape(c['page'])}' target=_blank>{html.escape(c['title'])}</a><br>"
                         f"{html.escape(c['author'])} · {html.escape(c['license'])}</figcaption></figure>")
        if not credits:
            parts.append("<p>Không tìm được ảnh phù hợp.</p>")
        parts.append("</div>")
    (STAGING / "preview.html").write_text("".join(parts), encoding="utf-8")


# ----------------------------------------------------------------------------------------------
# Áp dụng vào website + API
# ----------------------------------------------------------------------------------------------
def api(a, method, path, body=None):
    headers = {"X-Admin-Key": a.key}
    data = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body).encode()
    try:
        raw = http(a.api.rstrip("/") + path, method=method, headers=headers, data=data, retries=1)
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")[:300]
        sys.exit(f"API trả lỗi {e.code} cho {method} {path}: {detail}")
    except urllib.error.URLError as e:
        sys.exit(f"Không gọi được API {a.api} ({e.reason}). Hãy chạy MoveGo.API trước (dotnet run).")
    return json.loads(raw) if raw else None


def cmd_apply(a):
    if not STAGING.exists():
        sys.exit("Chưa có photo_staging/. Chạy bước `fetch` trước.")
    vehicles = {v["name"].lower(): v for v in api(a, "GET", "/api/admin/vehicles")}
    UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    all_credits = json.loads(CREDITS_FILE.read_text("utf-8")) if CREDITS_FILE.exists() else {}
    done = 0
    for veh in VEHICLES:
        folder = STAGING / veh["slug"]
        meta_path = folder / "credits.json"
        if not meta_path.exists():
            continue
        credits = [c for c in json.loads(meta_path.read_text("utf-8")) if (folder / c["file"]).exists()]
        if not credits:
            print(f"– {veh['name']}: không còn ảnh nào, giữ nguyên")
            continue
        current = vehicles.get(veh["name"].lower())
        if current is None:
            print(f"– {veh['name']}: không có trong database, bỏ qua")
            continue

        urls, used = [], []
        for n, c in enumerate(credits, 1):
            name = f"{veh['slug']}-{n:02d}.jpg"
            if not a.dry_run:
                shutil.copyfile(folder / c["file"], UPLOAD_DIR / name)
            urls.append(f"{UPLOAD_URL}/{name}")
            used.append({k: c[k] for k in ("title", "page", "author", "license", "licenseUrl")} | {"file": name})

        body = {
            "ownerId": current["ownerId"], "name": current["name"], "brand": current["brand"],
            "category": current["category"], "seats": current["seats"], "pricePerDay": current["pricePerDay"],
            "pickupAddress": current["pickupAddress"], "description": current["description"],
            "imageUrls": urls, "rangeKm": current.get("rangeKm"), "highlights": current["highlights"],
            "isActive": current["isActive"],
        }
        print(f"✓ {veh['name']}: {len(urls)} ảnh" + (" (chạy thử, chưa ghi)" if a.dry_run else ""))
        if not a.dry_run:
            api(a, "PUT", f"/api/admin/vehicles/{current['id']}", body)
            all_credits[veh["name"]] = used
        done += 1

    if not a.dry_run:
        CREDITS_FILE.write_text(json.dumps(all_credits, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nĐã cập nhật {done} xe. Tải lại website (Ctrl+F5) để xem. Ghi nguồn ảnh ở /nguon-anh.")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    f = sub.add_parser("fetch", help="tìm và tải ảnh về photo_staging/")
    f.add_argument("--max", type=int, default=6, help="số ảnh tối đa mỗi xe (mặc định 6, tối đa 8)")
    f.add_argument("--width", type=int, default=1400, help="chiều rộng ảnh tải về (px)")
    f.add_argument("--commons", default="https://commons.wikimedia.org/w/api.php")
    f.set_defaults(fn=cmd_fetch)

    s = sub.add_parser("apply", help="gắn ảnh trong photo_staging/ vào website + API")
    s.add_argument("--api", default="http://localhost:5100", help="địa chỉ MoveGo.API")
    s.add_argument("--key", required=True, help="Business:AdminKey (môi trường dev: movego-admin-dev)")
    s.add_argument("--dry-run", action="store_true", help="chỉ in ra việc sẽ làm")
    s.set_defaults(fn=cmd_apply)

    a = p.parse_args()
    if a.cmd == "fetch":
        a.max = max(1, min(a.max, 8))
    a.fn(a)


if __name__ == "__main__":
    main()
