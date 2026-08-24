import archiver from "archiver";
import { Readable } from "node:stream";
import { join } from "node:path";
import { rmSync } from "node:fs";
import { cleanupUpload, generateMigrated, repoRoot, saveUpload } from "@/lib/pipeline";

export const runtime = "nodejs";
export const maxDuration = 180;
export const dynamic = "force-dynamic";

function zipDir(dir: string, filename: string, onDone?: () => void) {
  const archive = archiver("zip", { zlib: { level: 6 } });
  archive.glob("**/*", { cwd: dir, ignore: ["**/bin/**", "**/obj/**"] });
  if (onDone) {
    archive.on("end", onDone);
    archive.on("error", onDone);
  }
  void archive.finalize();
  return new Response(Readable.toWeb(archive) as unknown as ReadableStream, {
    headers: {
      "Content-Type": "application/zip",
      "Content-Disposition": `attachment; filename="${filename}"`,
    },
  });
}

/** Bundled sample — committed migrated/ (same files generate emits). Instant download. */
export function GET() {
  const dir = join(repoRoot(), "samples", "vfp-inventory", "migrated");
  return zipDir(dir, "VfpInventory.zip");
}

/** Upload: extract + generate --build --spec, then zip migrated/. */
export async function POST(req: Request) {
  const form = await req.formData();
  const file = form.get("file");
  let source: string | undefined;
  if (file instanceof File && file.size > 0) {
    source = saveUpload(file.name, Buffer.from(await file.arrayBuffer()));
  }
  const result = await generateMigrated(source);
  cleanupUpload(source);
  if (!result.ok) return new Response(result.error, { status: 500 });
  return zipDir(result.migrated, "VfpInventory.zip", () => rmSync(result.work, { recursive: true, force: true }));
}
