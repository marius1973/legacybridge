import { NextResponse } from "next/server";
import { parseReport, sampleArtifacts, sampleReport, sampleSource } from "@/lib/pipeline";

export const runtime = "nodejs";

export function GET() {
  const src = sampleSource();
  const artifacts = sampleArtifacts();
  return NextResponse.json({
    ...parseReport(sampleReport()),
    source: src.text,
    sourceName: src.name,
    artifacts,
  });
}
