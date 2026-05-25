# The e-tjänst — web portal spec

The front door to Lampverket is a **Blazor e-tjänst** (digital government service) where citizens file applications. The whole comedic payload lives here: to turn on a lamp you must complete a form on a portal that looks exactly like a real Swedish authority's self-service site, then wait for a *handläggare* to issue a decision.

> Design brief: **maximalt myndighetstrist.** It should be instantly recognisable as a Swedish public e-tjänst — sober, formal, a little dated, mildly inconvenient. The joke is that all this ceremony stands between you and a light switch.

## Tech

- **Blazor Web App (.NET 8/9)**, interactive **Server** render mode — built-in SignalR makes it trivial to push the *beslut* to the page live once the handläggare has decided.
- Talks to `Lampverket.Agent` (the C# handläggare service) in-process. See [`ARCHITECTURE.md`](ARCHITECTURE.md).
- No client-side framework beyond Blazor; plain CSS for the design system (the trist look is easier to nail by hand than with a UI kit).

## Pages and flow

| Route | Page | Purpose |
| --- | --- | --- |
| `/` | Startsida | Authority landing page: who we are, "E-tjänster", driftinformation banner |
| `/logga-in` | BankID-inloggning | The login gag (mobilt BankID, fake) |
| `/ny-ansokan` | Ny ansökan | The application form (the heart of it) |
| `/kvitto/{diarienummer}` | Kvittens | Receipt: "Din ansökan har mottagits", diarienummer, beräknad handläggningstid |
| `/mina-arenden` | Mina ärenden | List of the citizen's ärenden from the diariet, with status |
| `/arende/{diarienummer}` | Ärende | Case detail; the *beslut* renders here (pushed live), then the light changes |
| `/tillganglighet` | Tillgänglighetsredogörelse | Accessibility statement parody (a required-looking footer link) |

**Happy path:** Startsida → Logga in med BankID → Ny ansökan → submit → Kvittens (diarienummer) → redirected to Ärende → "Under handläggning…" spinner → *beslut* appears via SignalR → the real light turns on.

## The application form (`/ny-ansokan`)

Formal, slightly over-asking. Suggested fields:

- **Personnummer** (ÅÅÅÅMMDD-XXXX) — present, "krävs för identifiering", pre-filled from the BankID gag so it's not actually a barrier.
- **Ärendetyp** (dropdown): *Tändning av belysning · Släckning av belysning · Justering av ljusstyrka · Uppspelning av media · Justering av volym*.
- **Berörd enhet** (dropdown) — populated from the configured device list (see [`DEVICES.md`](DEVICES.md)), e.g. "Banan (Sovrum)".
- **Önskad åtgärd** (conditional on ärendetyp) — e.g. a brightness slider, a volume value, a search term for media.
- **Motivering till ansökan** (textarea) — *"Beskriv ditt berättigade behov av åtgärden."* This text is what the handläggare weighs.
- **Önskat verkställighetsdatum** — a date picker defaulting to today, because of course it asks.
- **Försäkran** (checkbox) — *"Jag försäkrar på heder och samvete att uppgifterna är riktiga."*
- **Personuppgiftsnotis** (collapsible) — GDPR parody: *"Lampverket behandlar dina personuppgifter enligt dataskyddsförordningen samt 12 § lagen (2026:1) om skälig hemtrevnad. Du har rätt att begära registerutdrag."*
- Submit button: **"Skicka in ansökan"**. A secondary **"Avbryt"** link.

On submit, the form maps to an `Ansokan` model and is handed to the handläggare. The page navigates to the kvittens with the assigned `diarienummer`.

## Signature gags (these are the funny bits — keep them)

- **Driftinformation / öppettider banner** at the top: *"Observera: Lampverket har fika fredagar kl. 14:00–15:00. Ansökningar som inkommer under denna tid avslås automatiskt."* (Wired to the fikahelgd rule in [`BUREAUCRACY.md`](BUREAUCRACY.md).)
- **Köplats**: the kvittens shows *"Du är nummer 4 i kön. Beräknad handläggningstid: 8 sekunder."* — the absurdity of a queue for a light switch.
- **BankID gag**: a real-looking "Logga in med Mobilt BankID" screen with the QR/animation styling, that just lets you straight in.
- **Diarienummer everywhere**: shown on the kvittens, in Mina ärenden, in the URL, and in the beslut.
- **Tillgänglighetsredogörelse** and **"Den här webbplatsen använder kakor"** banner — the unmistakable furniture of a Swedish gov site.
- **Avslag with appeal text**: when rejected, the Ärende page shows the *"Hur man överklagar"* section with a working "Överklaga beslut"-button that re-opens the case.

## Design system — "maximalt myndighetstrist"

Aim for the visual language of a sober Swedish authority portal. Hand-rolled CSS; system fonts.

**Colour**

| Token | Value (suggested) | Use |
| --- | --- | --- |
| `--lv-blue` | `#1b4f72` | Header bar, headings, primary links |
| `--lv-blue-dark` | `#143d57` | Hover, top border accent |
| `--lv-grey-bg` | `#f4f4f2` | Page background (slightly off-white, never pure) |
| `--lv-grey-line` | `#cfcfca` | Hairlines, table borders, field borders |
| `--lv-text` | `#222222` | Body text |
| `--lv-yellow` | `#fbe6a2` | Driftinformation/öppettider banner background |
| `--lv-success` | `#2e7d32` | "Mottagen" / bifall status |
| `--lv-reject` | `#9b2c2c` | Avslag status |

Muted, low-saturation, high-contrast. No gradients, no shadows beyond a 1px line, no rounded corners (or 2px at most).

**Type**

- System sans stack: `"Segoe UI", Arial, Helvetica, sans-serif`. Deliberately plain.
- Generous line-height, smallish body (15px), uppercase section labels with letter-spacing for the formal feel.

**Layout**

- Fixed-width content column (~760px) centred on a grey page — the classic narrow gov layout.
- A coloured top border strip + a header bar with the **Lampverket** wordmark and a small crest/seal (a stylised lamp or the banana mascot as the unofficial seal).
- Breadcrumbs under the header: *Start / E-tjänster / Ny ansökan*.
- Footer with org-nr (`Org.nr 202600-0001`), öppettider, postadress (fiktiv), and the accessibility + cookies links.

**Components**

- Form fields: label above, 1px bordered input, helptext in grey italics below. Required fields marked with a red asterisk and the dreaded *"(obligatoriskt)"*.
- The **beslut** rendered in a bordered "document" card with the formal template from [`BUREAUCRACY.md`](BUREAUCRACY.md#the-beslut-template), monospace for the field labels.
- Status pills: *Inkommet*, *Under handläggning*, *Beslutat*, *Verkställt*, *Avslag*.

## Mapping form → ansökan → action

1. Form submit builds an `Ansokan { Personnummer, Arendetyp, BerordEnhet, OnskadAtgard, Motivering, OnskatDatum }`.
2. `HandlaggareService` registers it (assigns `diarienummer`, writes `Inkommet`), reads current device state, asks Claude for a `beslut`, and on *bifall* calls Home Assistant.
3. The page subscribes (SignalR) to the ärende and renders the `beslut` the moment it's issued; the light changes in the room.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the service-level detail and [`ROADMAP.md`](ROADMAP.md) for the build order.
