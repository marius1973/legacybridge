import { NextResponse } from "next/server";
import { parseReport, sampleReport } from "@/lib/pipeline";

export const runtime = "nodejs";

export function GET() {
  const parsed = parseReport(sampleReport());
  return NextResponse.json(parsed);
}
