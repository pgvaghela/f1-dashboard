// Visual metadata for standings rows: team colours/badges and nationality flags.
// Kept on the frontend so it can be tweaked without a redeploy of the API.

export interface TeamMeta {
  color: string;
  abbr: string;
}

const DEFAULT_TEAM: TeamMeta = { color: '#7a7a7a', abbr: '' };

// Matched by substring (case-insensitive) so Ergast naming variants
// ("Red Bull" vs "Red Bull Racing", "RB F1 Team" vs "Racing Bulls") all resolve.
const TEAM_RULES: { match: string[]; meta: TeamMeta }[] = [
  { match: ['mercedes'], meta: { color: '#27F4D2', abbr: 'MER' } },
  { match: ['ferrari'], meta: { color: '#E8002D', abbr: 'FER' } },
  { match: ['mclaren'], meta: { color: '#FF8000', abbr: 'MCL' } },
  { match: ['racing bull', 'rb f1', 'alphatauri'], meta: { color: '#6692FF', abbr: 'RB' } },
  { match: ['red bull'], meta: { color: '#3671C6', abbr: 'RBR' } },
  { match: ['aston martin'], meta: { color: '#229971', abbr: 'AMR' } },
  { match: ['alpine'], meta: { color: '#00A1E8', abbr: 'ALP' } },
  { match: ['williams'], meta: { color: '#1868DB', abbr: 'WIL' } },
  { match: ['haas'], meta: { color: '#B6BABD', abbr: 'HAA' } },
  { match: ['kick sauber', 'sauber'], meta: { color: '#52E252', abbr: 'SAU' } },
  { match: ['audi'], meta: { color: '#009597', abbr: 'AUD' } },
  { match: ['cadillac'], meta: { color: '#C8102E', abbr: 'CAD' } }
];

export function teamMeta(teamName: string | undefined | null): TeamMeta {
  if (!teamName) {
    return DEFAULT_TEAM;
  }
  const lower = teamName.toLowerCase();
  const found = TEAM_RULES.find((rule) => rule.match.some((m) => lower.includes(m)));
  if (found) {
    return found.meta;
  }
  // Fall back to initials from the team name.
  const abbr = teamName
    .split(/\s+/)
    .map((w) => w[0])
    .join('')
    .slice(0, 3)
    .toUpperCase();
  return { ...DEFAULT_TEAM, abbr };
}

// Ergast reports nationalities as demonyms; map them to ISO 3166-1 alpha-2 codes for flagcdn.
const NATIONALITY_TO_CC: Record<string, string> = {
  british: 'gb',
  english: 'gb',
  scottish: 'gb',
  german: 'de',
  dutch: 'nl',
  spanish: 'es',
  mexican: 'mx',
  monegasque: 'mc',
  australian: 'au',
  finnish: 'fi',
  french: 'fr',
  canadian: 'ca',
  japanese: 'jp',
  thai: 'th',
  danish: 'dk',
  chinese: 'cn',
  italian: 'it',
  american: 'us',
  austrian: 'at',
  'new zealander': 'nz',
  brazilian: 'br',
  argentine: 'ar',
  argentinian: 'ar',
  belgian: 'be',
  swiss: 'ch',
  swedish: 'se',
  russian: 'ru',
  polish: 'pl',
  portuguese: 'pt',
  irish: 'ie',
  indian: 'in'
};

export function flagUrl(nationality: string | undefined | null): string | null {
  if (!nationality) {
    return null;
  }
  const cc = NATIONALITY_TO_CC[nationality.trim().toLowerCase()];
  return cc ? `https://flagcdn.com/w80/${cc}.png` : null;
}

// Official F1 driver headshots, keyed by 3-letter code. Sourced from the OpenF1
// driver feed (formula1.com media CDN). Covers the 2023-2026 grids.
const DRIVER_HEADSHOTS: Record<string, string> = {
  ALB: 'https://www.formula1.com/content/dam/fom-website/drivers/A/ALEALB01_Alexander_Albon/alealb01.png.transform/1col/image.png',
  ALO: 'https://www.formula1.com/content/dam/fom-website/drivers/F/FERALO01_Fernando_Alonso/feralo01.png.transform/1col/image.png',
  ANT: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/A/ANDANT01_Andrea%20Kimi_Antonelli/andant01.png.transform/1col/image.png',
  BEA: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/O/OLIBEA01_Oliver_Bearman/olibea01.png.transform/1col/image.png',
  BOR: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/G/GABBOR01_Gabriel_Bortoleto/gabbor01.png.transform/1col/image.png',
  BOT: 'https://www.formula1.com/content/dam/fom-website/drivers/V/VALBOT01_Valtteri_Bottas/valbot01.png.transform/1col/image.png',
  COL: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/F/FRACOL01_Franco_Colapinto/fracol01.png.transform/1col/image.png',
  GAS: 'https://www.formula1.com/content/dam/fom-website/drivers/P/PIEGAS01_Pierre_Gasly/piegas01.png.transform/1col/image.png',
  HAD: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/I/ISAHAD01_Isack_Hadjar/isahad01.png.transform/1col/image.png',
  HAM: 'https://www.formula1.com/content/dam/fom-website/drivers/L/LEWHAM01_Lewis_Hamilton/lewham01.png.transform/1col/image.png',
  HUL: 'https://www.formula1.com/content/dam/fom-website/drivers/N/NICHUL01_Nico_Hulkenberg/nichul01.png.transform/1col/image.png',
  LAW: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/L/LIALAW01_Liam_Lawson/lialaw01.png.transform/1col/image.png',
  LEC: 'https://www.formula1.com/content/dam/fom-website/drivers/C/CHALEC01_Charles_Leclerc/chalec01.png.transform/1col/image.png',
  LIN: 'https://media.formula1.com/d_driver_fallback_image.png/content/dam/fom-website/drivers/A/ARVLIN01_Arvid_Lindblad/arvlin01.png.transform/1col/image.png',
  MAG: 'https://www.formula1.com/content/dam/fom-website/drivers/K/KEVMAG01_Kevin_Magnussen/kevmag01.png.transform/1col/image.png',
  NOR: 'https://www.formula1.com/content/dam/fom-website/drivers/L/LANNOR01_Lando_Norris/lannor01.png.transform/1col/image.png',
  OCO: 'https://www.formula1.com/content/dam/fom-website/drivers/E/ESTOCO01_Esteban_Ocon/estoco01.png.transform/1col/image.png',
  PER: 'https://www.formula1.com/content/dam/fom-website/drivers/S/SERPER01_Sergio_Perez/serper01.png.transform/1col/image.png',
  PIA: 'https://www.formula1.com/content/dam/fom-website/drivers/O/OSCPIA01_Oscar_Piastri/oscpia01.png.transform/1col/image.png',
  RUS: 'https://www.formula1.com/content/dam/fom-website/drivers/G/GEORUS01_George_Russell/georus01.png.transform/1col/image.png',
  SAI: 'https://www.formula1.com/content/dam/fom-website/drivers/C/CARSAI01_Carlos_Sainz/carsai01.png.transform/1col/image.png',
  SAR: 'https://www.formula1.com/content/dam/fom-website/drivers/L/LOGSAR01_Logan_Sargeant/logsar01.png.transform/1col/image.png',
  STR: 'https://www.formula1.com/content/dam/fom-website/drivers/L/LANSTR01_Lance_Stroll/lanstr01.png.transform/1col/image.png',
  TSU: 'https://www.formula1.com/content/dam/fom-website/drivers/Y/YUKTSU01_Yuki_Tsunoda/yuktsu01.png.transform/1col/image.png',
  VER: 'https://www.formula1.com/content/dam/fom-website/drivers/M/MAXVER01_Max_Verstappen/maxver01.png.transform/1col/image.png',
  ZHO: 'https://www.formula1.com/content/dam/fom-website/drivers/G/GUAZHO01_Guanyu_Zhou/guazho01.png.transform/1col/image.png'
};

export function driverHeadshot(code: string | undefined | null): string | null {
  if (!code) {
    return null;
  }
  return DRIVER_HEADSHOTS[code.trim().toUpperCase()] ?? null;
}

// Official F1 constructor logos. The CDN organises them per season folder, so
// legacy teams point at the year they last raced. Newest entrants (Audi/Cadillac)
// have no logo published at this path yet, so they fall back to the team badge.
const TEAM_LOGO_BASE = 'https://media.formula1.com/content/dam/fom-website/teams';
const TEAM_LOGO_RULES: { match: string[]; path: string }[] = [
  { match: ['mercedes'], path: '2025/mercedes' },
  { match: ['ferrari'], path: '2025/ferrari' },
  { match: ['mclaren'], path: '2025/mclaren' },
  { match: ['red bull'], path: '2025/red-bull-racing' },
  { match: ['racing bull', 'rb f1'], path: '2025/racing-bulls' },
  { match: ['alphatauri'], path: '2023/alphatauri' },
  { match: ['aston martin'], path: '2025/aston-martin' },
  { match: ['alpine'], path: '2025/alpine' },
  { match: ['williams'], path: '2025/williams' },
  { match: ['haas'], path: '2025/haas' },
  { match: ['kick sauber', 'sauber'], path: '2025/kick-sauber' },
  { match: ['alfa romeo'], path: '2023/alfa-romeo' }
];

export function constructorLogo(teamName: string | undefined | null): string | null {
  if (!teamName) {
    return null;
  }
  const lower = teamName.toLowerCase();
  const found = TEAM_LOGO_RULES.find((rule) => rule.match.some((m) => lower.includes(m)));
  return found ? `${TEAM_LOGO_BASE}/${found.path}.png` : null;
}
