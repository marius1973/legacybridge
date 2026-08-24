import { NextResponse } from "next/server";
import { parseReport, sampleReport, sampleSource } from "@/lib/pipeline";

export const runtime = "nodejs";

export function GET() {
  const src = sampleSource();
  return NextResponse.json({ ...parseReport(sampleReport()), source: src.text, sourceName: src.name });
}
