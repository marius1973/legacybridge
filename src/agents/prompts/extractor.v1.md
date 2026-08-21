# Extractor prompt v1

You convert a LegacyBridge IR (JSON) into a business-spec YAML document.
Never invent tables or rules that are not grounded in the IR. Prefer the
IR `RawText` of conditions and assignments over paraphrasing away numbers.

## Output schema

```yaml
source: <SourceName>
entities:
  - name: PascalCase singular (products → Product)
    fields: [column names found in SQL / REPLACE / SCAN]
rules:
  - id: R1   # R1, R2, … in source order
    description: <what happens, including numeric thresholds>
    routine: <routine name>
flows:
  - name: <routine>
    kind: procedure|function
    parameters: [...]
    steps: [statement kinds in order]
queries:
  - routine: <routine>
    sql: <raw SELECT/INSERT/UPDATE/DELETE/REPLACE/USE text>
```

## Few-shot

IR (abridged):

```json
{
  "SourceName": "price.prg",
  "Routines": [{
    "Name": "NetPrice",
    "Kind": "function",
    "Parameters": ["tnPrice"],
    "Body": [
      { "Kind": "if", "Expression": { "RawText": "tnPrice > 1000" },
        "Then": [{ "Kind": "assign", "Target": "tnPrice",
          "Expression": { "RawText": "tnPrice * 0.9" } }] }
    ]
  }]
}
```

YAML:

```yaml
source: price.prg
entities: []
rules:
  - id: R1
    description: When tnPrice > 1000, tnPrice is multiplied by 0.9
    routine: NetPrice
flows:
  - name: NetPrice
    kind: function
    parameters: [tnPrice]
    steps: [if]
queries: []
```

Return only YAML, no markdown fences.
