import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { analyzeLegacy, generateDotnet, repoRoot, runEquivalence } from "./cli.ts";

const sample = "samples/vfp-inventory/legacy";

export function createServer(): McpServer {
  const server = new McpServer({ name: "legacybridge", version: "0.5.0" });
  server.registerTool(
    "analyze_legacy",
    {
      description: "Parse Visual FoxPro (.prg) source into a summarized IR (routine names, parameters, statement counts).",
      inputSchema: { path: z.string().describe("File or directory of .prg files") },
    },
    async ({ path }) => text(await analyzeLegacy(path)),
  );
  server.registerTool(
    "generate_dotnet",
    {
      description: "Generate a compiling .NET 8 Clean Architecture solution from VFP source.",
      inputSchema: {
        path: z.string().describe("File or directory of .prg files"),
        output: z.string().optional().describe("Output directory (default generated/mcp)"),
      },
    },
    async ({ path, output }) => text(await generateDotnet(path, output)),
  );
  server.registerTool(
    "run_equivalence",
    {
      description: "Run IR oracle vs migrated .NET and return the equivalence rate plus report header.",
      inputSchema: { path: z.string().describe("File or directory of .prg files") },
    },
    async ({ path }) => text(await runEquivalence(path)),
  );
  return server;
}

function text(s: string) {
  return { content: [{ type: "text" as const, text: s }] };
}

if (process.argv.includes("--self-test")) {
  const root = repoRoot();
  const out = await analyzeLegacy(sample, root);
  if (!out.includes("CalcStockValue")) {
    console.error(out);
    process.exit(1);
  }
  console.error("mcp self-test ok");
  process.exit(0);
}

const server = createServer();
await server.connect(new StdioServerTransport());
