# SpriteGen

Generates pixel art sprites by prompting a language model for a palette + a grid of palette indices, then rendering the result. Text models give exact dimensional control and pixel-perfect output that image generators can't match for this use case.

## How it works

A sprite is requested as structured JSON: a `palette` (array of hex colors) and `rows` (an array of rows, each an array of integer indices into the palette). The model reasons about the grid as data, so output is crisp, dimension-exact, and palette-constrained. The generation loop feeds a sprite back to the model for text-driven refinement ("make the legs longer", "darker uniform").

## Architecture

Hexagonal (ports & adapters):

- **SpriteGen.Domain** — models (`SpriteGrid`, `Palette`, `Sprite`, `SpriteDimensions`) and ports (`IGenerationPort`, `IRendererPort<T>`). No external dependencies.
- **SpriteGen.Application** — orchestration (`SpriteGenerationService`). Depends only on Domain.
- **SpriteGen.Infrastructure** — LLM adapters (`ClaudeAdapter`, `GeminiAdapter`) sharing `LlmAdapterBase`. Implements `IGenerationPort`.
- **SpriteGen.Console** — entry point, `ConsoleRenderer`, wiring.

Adding a new model backend means writing one `CallApiAsync` override. Adding a new output target (PNG, Unity) means one new `IRendererPort<T>` implementation. Neither touches the domain.

## Configuration

`SpriteGen.Console/appsettings.json`:

```json
{
  "Generator": "Claude",
  "SpriteWidth": 32,
  "SpriteHeight": 32
}
```

- `Generator` — `"Claude"` or `"Gemini"`.
- `SpriteWidth` / `SpriteHeight` — sprite dimensions. Default 32×32 if omitted.

## API keys

Keys are read from environment variables, never committed:

- Claude: `ANTHROPIC_API_KEY`
- Gemini: `GEMINI_API_KEY`

Set the one matching your selected generator:

```bash
# Windows (persists; restart terminal/IDE after)
setx ANTHROPIC_API_KEY "your-key-here"

# macOS / Linux (add to shell profile to persist)
export ANTHROPIC_API_KEY="your-key-here"
```

## Running

```bash
dotnet run --project SpriteGen.Console
```

Menu:

1. **Prompt** — describe a sprite, then refine iteratively. `new` resets, `menu` returns.
2. **Load** — render a saved sprite JSON (must include `width`, `height`, `palette`, and a flat `pixels` array).
3. **Quit**.

## Requirements

- .NET 10 SDK
- An API key for the selected model backend

## Status

Proof of concept. Single-sprite generation and refinement working. Planned: PNG export, spritesheets, and animation (walk cycles, idle) with palette locking across frames.
