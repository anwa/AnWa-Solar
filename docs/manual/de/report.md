# Bericht

Die Registerkarte **Bericht** enthält einen Markdown-Bericht der aktuellen Auslegung in der Sprache der Anwendung. Er wird bei jeder Änderung aktualisiert.

Der Bericht enthält:

- die Projektdaten (Name, Kunde, Adresse, Notiz),
- die Parameter (Zelltemperaturbereich, Sicherheitsabschlag),
- die Daten des Wechselrichters und die Hinweise zur Berechnung mit der Temperaturformel,
- die wirksamen Grenzen mit Sicherheitsabschlag,
- pro MPPT das Modul, die Einstellungen, den verwendeten Vmpp-Koeffizienten, die Ergebnisse, die zulässigen Anzahlen und die verletzten Grenzen,
- die AC-Seite, Kabel und Sicherungen und den Batteriespeicher.

Ausgeschaltete MPPTs erscheinen als „Ausgeschaltet“ ohne Werte.

## Markdown und Vorschau

**Vorschau** und **Markdown** über dem Bericht wechseln zwischen der formatierten Vorschau (zuerst angezeigt) und dem Markdown-Quelltext. Die Vorschau zeigt Formeln gesetzt; sie funktioniert ohne Internetverbindung und folgt dem hellen oder dunklen Modus der Anwendung.

## Kopieren, speichern und exportieren

- **Markdown kopieren** kopiert den Markdown-Quelltext in die Zwischenablage, zum Beispiel in ein Dokument oder ein Ticket.
- **Markdown speichern** speichert ihn als `.md`-Datei; der vorgeschlagene Name ist `AnWa-Solar-Report.md`.
- **HTML exportieren** speichert den Bericht als einzelne `.html`-Datei. Die Formeln sind gerendert und die Schriften eingebettet, die Datei öffnet sich also in jedem Browser ohne Internetverbindung und ohne weitere Dateien. Sie verwendet immer die hellen Farben.
- **PDF exportieren** speichert den Bericht als PDF-Datei in A4 mit Titel, Datum und Seitenzahlen, in hellen Farben. Es braucht die Berichtsvorschau (Microsoft Edge WebView2 Runtime).

Beide Exporte wechseln zuerst zur Vorschau, damit die Formeln gerendert sind. Ohne WebView2 Runtime behält **HTML exportieren** die Formeln als Quelltext. Die Speichern-Dialoge starten im Exportordner aus den [Einstellungen](settings.md#einstellungsseite).
