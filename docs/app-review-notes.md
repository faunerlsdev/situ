# App Review notes

SITU is a location-based community app for Colombia. People post what is happening nearby (traffic, safety, local alerts), see those posts on a map, and can watch or start a live stream from the mobile app. The web app can only watch and comment. The interface is in Spanish.

## How to test

1. Install the build and open SITU.
2. Sign in with `<DEMO_USER>` / `<DEMO_PASSWORD>`. This account already has posts near Bogotá (with photos), comments, and one finished live stream. Please do not delete it.
3. To test account deletion, sign in with `<DEMO_DELETE_USER>` / `<DEMO_DELETE_PASSWORD>` and open Mi SITU → Mi cuenta → Eliminar mi cuenta. Confirming asks for the password, signs the person out, revokes that session, and marks the account for deletion. The profile, posts, comments and live streams are hidden right away. The rows stay stored. They can sign in again within 14 days and cancel, and that content is shown again. If they do not cancel, the server permanently deletes the account and its content after 14 days. It is not only a logout.
4. Map: allow location, then deny it on a fresh install. Without location the map stays centered on Bogotá (or the account city, if one was chosen). The dialog offers Abrir ajustes and can be dismissed with Ahora no. The map stays usable.
5. Open a post, use the menu to report it, and block the author. A live stream that is on the air can be reported with a reason from the viewer. Blocking a user hides that person’s posts and live streams, including streams that already ended, for the person who blocked them. Reports are reviewed within 24 hours. Content involving a minor is hidden immediately.
6. Registration asks the person to accept the Terms and the Privacy Policy before the account is created. Date of birth is required and the label says it is used to verify the minimum age. Phone, province and city are optional. The city label says it is optional and that the app shows that place first. The app does not ask for gender. The public terms page still serves version 1.0 until version 1.1 is published. Terms v1.1 section 20.X is a zero-tolerance rule: objectionable content and abusive users are prohibited (offensive, defamatory, sexual, violent, discriminatory or threatening content, exposing other people’s personal data, and harassment). Anyone can report content or block a user in the app. SITU reviews reports within 24 hours and, when it confirms a violation, removes the content and may suspend or permanently cancel the author’s account. Child safety standards: https://somossitu.com/seguridad-infantil
7. Help and contact: Mi cuenta, or https://ayuda.somossitu.com/contacto (soporte@somossitu.com).

## Permissions

- Location when in use: center the map, measure distance, and, only if the person turns it on, share an approximate point. Not used in the background.
- Camera, photo library, and microphone: only when the person attaches a photo or video, or starts a live stream.
- Face ID: unlock the session on this device.
- Notifications: only after the person opts in.

The app does not use the advertising identifier and does not track users across apps.

## Login

Accounts are email and password only. There is no Google or Facebook sign-in, so Sign in with Apple is not used.

## Minors

The minimum age in Colombia is 14, checked with the date of birth at registration. Anyone under 18 stays pending until a parent or guardian opens the email link, verifies that email, and authorizes the account. A minor has a private profile, does not appear on the public map, cannot upload their own photo, and cannot go live unless that guardian authorizes it.

## Moderation

Users must accept the Terms before registering. The server rejects a post, a comment or a live title that contains insults, sexual content, direct threats, or another person's phone, ID number or street address. The rejection is shown next to the field. People can report a post, a comment, a live stream or a profile, and block another user. A live stream is reported with a reason. Blocking hides that person’s posts and live streams (in progress and already ended) for the blocker. Terms v1.1 include a zero-tolerance clause. The moderation team reviews reports within 24 hours, removes the content when the violation is confirmed, and can suspend or permanently cancel the account. This does not replace the police. Emergencies in Colombia should go to 123.
