# Progress

## Step 1. Keep array attributes

Span attributes are `JsonNode` values: scalars stay text as before, arrays are JSON arrays whose items keep their type (`["first","second"]`, `[1,2]`), as decided in `../qa.md` 116. `ogooreck_find_spans` matches an array item by item. Non-finite doubles come back as `"NaN"`, `"Infinity"`, `"-Infinity"`; array items of unsupported types (bytes, maps) are dropped, like unsupported scalars. `CollectedSpan.Attribute(key, fallback)` gives the readers the text of an attribute.

Deviation: the fixture's `own work` span also sets `own.attempts` (`int[]`) and `own.unknown` (`NaN`), to test the typed items and the `NaN` case. Review: `reviews/20261127T150000_step1_review.md`, clean.
