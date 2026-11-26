# RatioBackup – Protokoll Handy ↔ PC

Verbindlich für `android/` und `pc/`. Alle Pfade sind relativ zu `/storage/emulated/0`, mit `/` getrennt (z. B. `DCIM/Camera/IMG_1.jpg`). Zeiten in Millisekunden seit 1970 (UTC). JSON-Felder in camelCase.

## Gerätesuche (UDP 47320)
1. Handy sendet Broadcast an `255.255.255.255:47320` mit dem Inhalt `RATIOBACKUP?` (ASCII).
2. PC antwortet per Unicast an den Absender mit JSON:
   `{"name":"<PC-Name>","port":47321,"fingerprint":"<SHA-256 des Zertifikats, hex, klein>"}`

## HTTPS-API (TCP 47321)
Der PC nutzt ein selbstsigniertes Zertifikat. Das Handy akzeptiert nur das Zertifikat, dessen Fingerabdruck es beim Koppeln gespeichert hat.

### `POST /pair` (ohne Token)
Anfrage `{"code":"123456","deviceName":"Xiaomi 18 Pro"}`
- `200 {"token":"<base64, 32 Byte>","proof":"<hex(HMAC-SHA256(key=UTF8(code), msg=UTF8(fingerprint)))>"}`
- `403` falscher oder abgelaufener Code (5 Minuten gültig, einmal verwendbar, nach 5 Fehlversuchen ungültig)

Das Handy prüft `proof` gegen den Fingerabdruck aus der Gerätesuche und speichert Token + Fingerabdruck nur, wenn er stimmt. Neues Koppeln macht das alte Token ungültig.

Alle folgenden Endpunkte verlangen `Authorization: Bearer <token>`, sonst `401`.

### `GET /status`
`200 {"usedBytes":0,"quotaBytes":0,"trashBytes":0,"lastBackupUtc":"2026-11-26T19:04:00Z"|null,"pendingDeletionReview":false}`

### `POST /manifest`
Anfrage `{"roots":["DCIM",…],"files":[{"path":"DCIM/a.jpg","size":123,"mtimeMs":1764180000000}]}`
- `roots`: nur Wurzeln, die am Handy existieren und lesbar sind. Der PC löscht nur innerhalb dieser Wurzeln.
- Antwort `200 {"needed":[{"path":"DCIM/a.jpg","offset":0}],"trashed":0,"heldBackDeletions":0}`
  - `offset` > 0: ein Teil liegt schon auf dem PC, ab dort weiter senden.
  - Fehlende Dateien kommen in den Papierkorb, außer bei Massenlöschung (≥ 50 Dateien **und** > 20 % einer Wurzel): dann `heldBackDeletions` > 0 und Bestätigung am PC.

### `PUT /files?path=<url-encoded>&size=<long>&mtimeMs=<long>&offset=<long>`
Body: Bytes der Datei ab `offset`. Der Request, der die Datei vollständig macht, trägt `X-Sha256: <hex>` (SHA-256 der ganzen Datei).
- `200` Datei fertig
- `206 {"offset":n}` Teil gespeichert (Datei noch nicht vollständig)
- `400` unzulässiger Pfad
- `409 {"offset":n}` falscher Offset; n = vorhandene Bytes
- `422` Größe oder Prüfsumme falsch; Teil verworfen, neu ab 0
- `507` Speicherlimit erreicht (Papierkorb wurde bereits geleert)

### `POST /complete`
`200` – setzt `lastBackupUtc`, löscht Papierkorb-Tage älter als 30 Tage.
