# Steam Workshop

What the [Release workflow](../.github/workflows/release.yml) uploads alongside the mod. See
[Building from Source](../docs/src/content/docs/development/building.md#release-to-the-steam-workshop).

| File | |
|---|---|
| `description.bbcode` | The Workshop description, in [Steam BBCode](https://steamcommunity.com/comment/ForumTopic/formattinghelp). It replaces the description on Steam on every release, so edit it here. |
| `image.png` | The cover image, under 1 MB. Optional, but without it the item has no cover. |

The title is set in the workflow, and the change note is the release's notes. Tags, extra preview images and
visibility (after the first upload) are managed on the Workshop page.
