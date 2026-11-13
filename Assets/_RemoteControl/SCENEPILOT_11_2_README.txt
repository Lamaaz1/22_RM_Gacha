ScenePilot Unity package 11.2
================================

This update keeps the existing download, cache, local-sprite fallback,
static-panel ownership and sequence behavior. It adds support for the website's
Rate Us store URLs and two game-promotion static panels.

RATE US
-------
Panel ID: rate_us

The optional JSON field storeUrls accepts separate HTTPS URLs for android and
ios. On the matching platform, a non-empty server URL is used first. If the
server URL is empty, ScenePilot keeps the existing local behavior:

- overrideStoreUrl, when assigned
- Android: Application.identifier (package name)
- iOS: appleAppId

MORE GAMES
----------
Panel ID: more_games
Binding keys:
bg, panel, title, game_1, game_2, game_3, game_4, game_5, game_6, close_button

Each game_1 through game_6 element can receive an image and an HTTPS url. Bind
its Target Object to a UI Button or UI Graphic. ScenePilot adds only its own URL
listener and preserves existing local Button listeners. A blank URL keeps the
local Unity action. The close_button advances to the next enabled static panel.

PLAY OTHER GAME
---------------
Panel ID: play_other_game
Binding keys:
bg, panel, game_image, play_button, close_button

The play_button element can receive an HTTPS url. Bind it to a UI Button or UI
Graphic. The close_button advances to the next enabled static panel. The game
image and every other image continue to use the normal rule: valid downloaded
sprite first, otherwise the original local Unity sprite.

SETUP
-----
Create or use your own panel UI, add RemoteStaticPanel to its root, assign the
exact panel ID above, assign Panel Root, then add the listed element bindings.
The component's "Fill empty panel with standard element keys" context command
can create the binding rows. Assign each row to the corresponding local Image,
Button, TextMeshProUGUI or RectTransform.

Only absolute HTTPS destinations are accepted. Invalid remote URL data rejects
the candidate config, leaving the last valid cached/local behavior in place.
