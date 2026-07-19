The `media` feature (mobile side) lists the signed-in user's stored files with
their sizes, uploads a picked image, and deletes files.

- `data/media_repository.dart` calls the JWT-protected `/v1/media` endpoints,
  attaching the auth feature's access token via `SecureTokenStore`. Upload uses a
  `MultipartRequest` (`file` part); it uses plain authenticated HTTP, **not** the
  ALE `secureSend` client, because the server scopes rows per user by RLS and
  needs the authenticated identity.
- `bloc/media_cubit.dart` holds all I/O behind an immutable `MediaState`
  (`load` / `upload` / `delete`); the view only renders.
- `views/media_page.dart` uses `image_picker` to choose an image, reads its bytes,
  and uploads. Downloading raw bytes goes through `downloadUri`, which still needs
  the bearer token — attach it if you render thumbnails.
- Deps `image_picker` and `http_parser` are added to `pubspec.yaml` via the
  `pubspec-deps` anchor. On iOS/macOS add the photo-library usage keys to
  `Info.plist` per the image_picker setup.
- Requires the `auth` feature (for the session token) — enable them together.
