import { parseReport, runPipeline, saveUpload } from "@/lib/pipeline";

export const runtime = "nodejs";
export const maxDuration = 180;
export const dynamic = "force-dynamic";

export async function POST(req: Request) {
  const form = await req.formData();
  const file = form.get("file");
  let source: string | undefined;
  if (file instanceof File && file.size > 0) {
    const buf = Buffer.from(await file.arrayBuffer());
    source = saveUpload(file.name, buf);
  }

  const encoder = new TextEncoder();
  const stream = new ReadableStream({
    async start(controller) {
      const send = (obj: unknown) => controller.enqueue(encoder.encode(`data: ${JSON.stringify(obj)}\n\n`));
      try {
        const result = await runPipeline(
          (step, status, detail) => send({ step, status, detail }),
          source,
        );
        if (result.ok) send({ report: parseReport(result.artifacts!.report), artifacts: result.artifacts });
        else send({ error: result.report.slice(-2000) });
      } catch (e) {
        send({ error: e instanceof Error ? e.message : String(e) });
      }
      controller.close();
    },
  });

  return new Response(stream, {
    headers: {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache",
      Connection: "keep-alive",
    },
  });
}
