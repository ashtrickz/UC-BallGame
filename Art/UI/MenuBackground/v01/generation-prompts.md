# Menu background — generation record

Generated with the built-in ImageGen tool. Final selected file: `menu-background.png`.
The final image has an exact width:height ratio of 1:2. It contains only the environment; the character, shadow, logo and interface are separate layers.
Reference: the approved menu mockup generated in this conversation.

## Environment generation

```text
Use case: precise-object-edit.
Asset type: production-ready clean background plate for a Unity portrait mobile game's main menu.
Input image 1 is the approved menu mockup and the visual style/reference target. Create a clean ENVIRONMENT-ONLY background based on this image, recomposing and expanding the scenery into a slightly wider portrait canvas with an exact width:height ratio of 1:2. Target output 1536 x 3072 pixels if supported.

PRIMARY TASK: Remove EVERY foreground/UI element completely and naturally reconstruct the scenery behind them. Remove the main large green slime and its shadow, all other slimes, the distant glowing green slime-colony/mouth/portal, all title lettering, all words and numbers, the settings button and icon, all color swatches, all buttons, all selection rings and check marks, all labels and their shadows. There must be absolutely NO text, NO character, NO interface, NO logo, NO watermark, NO visible empty UI panels, NO baked-in oval character shadow. Do not simply blur or cover these objects: paint a continuous coherent canyon environment behind them.

PRESERVE THE ESTABLISHED STYLE: warm, cheerful, premium casual 3D-cartoon game background, soft rounded sandstone canyon rocks, broad terracotta/salmon/ochre facets, clean stylized shapes, warm sandy peach ground with sparse large gentle cracks, sparse little green succulents and golden dry grass, a few coral-pink and amethyst-purple faceted crystals only along the far lateral edges, clear bright blue sky with small soft distant clouds. Consistent sunlight from upper left and soft rightward shadows. Keep the illustration quality, warm palette and soft material shading of the reference, rather than photorealism, flat vector art or realistic desert photography.

COMPOSITION FOR UI OVERLAY:
- Full-bleed portrait illustration, exact 1:2 ratio, slightly wider field of view than the reference. No borders.
- A clear sandy path leads from the lower center toward a gentle distant canyon bend, horizon around 36–39% from the top.
- The upper center is quiet blue sky suitable for a logo to be overlaid separately.
- The entire central 60% of the image width, especially y=40–95%, must remain OPEN unobstructed softly textured sandy ground. This is where a separately rendered 3D slime, color presets and buttons will later go. No center crystal pile, no pedestal, no focal rock, no bush in the central area, no hard dark diagonal shadow cutting through it.
- In the middle around y=48–59%, leave an especially calm broad gently receding sandy patch where the live 3D slime can stand. Match the moderate elevated third-person view of the reference.
- Rocks, succulents and small crystals form a relaxed natural frame near the left and right edges. Keep crystal accents modest and close to the edges; do not introduce a giant decorative foreground cluster.
- The lower half should have low visual contrast and restrained ground detail so cream/coral UI is easy to read.
- Make the composition safe for centered aspect-fill / Envelope cropping: decorative outer edges and the top/bottom can be cropped without losing a required subject. Do not bake any UI or important object near the borders. The central portion should still read as a coherent canyon path if cropped for a portrait tablet.
- Crisp, clean smooth game-art rendering, distant scenery gently softened for depth, no globally blurry image, no painterly noise.

Output ONLY the finished clean environment background image.
```

## Final aspect-ratio correction

```text
Use case: precise-object-edit.
Input image: clean cartoon canyon menu background, currently too narrow at a width:height ratio of 1:3.
Make ONE targeted correction: EXPAND THE CANVAS HORIZONTALLY until the result has width:height EXACTLY 1:2 (0.5). Output a 1024-pixel-wide by 2048-pixel-tall portrait image. TWO squares stacked vertically. The height must be exactly twice the width. Do NOT output 1:3. Do NOT retain the input's narrow aspect ratio. The output must be noticeably WIDER than the input.

Outpaint additional matching canyon environment on BOTH the left and right sides. Maintain the existing scene's perspective, sunny sky, rock style, warm peach sandy ground, edge vegetation, modest pink/purple crystals and gentle central path. Keep the content's proportions natural: do not stretch the image or make the rocks wider. Preserve the same visual style and lighting. Continue the environment naturally into the expanded areas.

This is a production background plate for a Unity menu. Preserve the wide empty central area for a separately rendered character and UI to be added later. Keep the center uncluttered and gently textured, with small environmental details near the outer edges. No text, no logo, no buttons, no icons, no interface, no characters, no slime, no slime colony, no portal, no baked character shadow. Full-bleed artwork with no frame and no margins. Aspect ratio 1:2 is the essential correction.
```
