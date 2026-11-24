# Report

The **Report** tab contains a Markdown report of the current design. It is updated with every change.

The report contains:

- the parameters (cell temperature range, safety margin),
- the inverter data and the calculation notes with the temperature formula,
- the effective limits with the safety margin,
- per MPPT the module, the settings, the Vmpp coefficient used, the results, the allowed counts and the violated limits.

Disabled MPPTs are listed as "Disabled" without values.

## Markdown and preview

**Preview** and **Markdown** above the report switch between the formatted preview (shown first) and the Markdown source. The preview shows formulas typeset; it works without an internet connection and follows the light or dark mode of the application.

## Copy, save and export

- **Copy Markdown** copies the Markdown source to the clipboard, for example into a document or an issue.
- **Save Markdown** saves it as a `.md` file; the suggested name is `AnWa-Solar-Report.md`.
- **Export HTML** saves the report as a single `.html` file. The formulas are rendered, and the fonts are embedded, so the file opens in any browser without an internet connection and without further files. It always uses the light colours.
- **Export PDF** saves the report as a PDF file in A4 with the title, the date and page numbers, in light colours. It needs the report preview (Microsoft Edge WebView2 Runtime).

Both exports switch to the preview first, so that the formulas are rendered. Without the WebView2 Runtime, **Export HTML** keeps the formulas as source text.
