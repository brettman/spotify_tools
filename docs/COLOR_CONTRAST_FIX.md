# Color Contrast Fix - Playlists Page

## Issue
White text on white background in:
1. Genre/Cluster filter components
2. Playlist detail modal (header, body, table)

## Root Cause
The Spotify theme CSS uses dark backgrounds with white text globally, but Bootstrap modal and form components were using default light styling.

## Changes Made

### 1. SearchableMultiSelect Component Styling
**File:** `src/SpotifyTools.Web/Components/SearchableMultiSelect.razor`

Added comprehensive dark theme styling with Spotify theme variables:
- **Dropdown list:** Dark background `var(--spotify-elevated, #181818)` with white text
- **Form controls:** Dark input backgrounds with proper border colors
- **Input focus states:** Spotify green border on focus
- **Placeholder text:** Subdued gray color
- **Hover states:** Highlight background on hover
- **Active items:** Green-tinted background for selected items

### 2. Playlist Detail Modal
**File:** `src/SpotifyTools.Web/Pages/Playlists.razor`

Applied inline styles to override Bootstrap defaults:

**Modal Header:**
- Background: Spotify green `#1DB954` (brand color)
- Text: Black for high contrast on green
- Close button: Black icon (using `filter: brightness(0)`)

**Modal Body:**
- Background: Spotify elevated `#181818`
- Text: White `#FFFFFF`

**Table Headers:**
- Background: Matches modal body
- Text: Secondary gray for subdued appearance
- Sticky positioning maintained

**Table Cells:**
- Primary text: White
- Secondary text (album, duration): Light gray
- Muted text (track numbers): Subdued gray

**Modal Footer:**
- Background: Dark elevated
- Border: Subtle dark border

## Color Palette Used

From `spotify-theme.css` variables:
```css
--spotify-elevated: #181818      (Main dark background)
--spotify-highlight: #282828     (Hover/active states)
--spotify-green: #1DB954          (Brand color/accents)
--text-primary: #FFFFFF           (Main text)
--text-secondary: #B3B3B3         (Secondary text)
--text-subdued: #6A6A6A           (Muted text)
--border-subtle: #282828          (Border colors)
```

## Fallback Values
All CSS variables include fallback hex values for browser compatibility:
```css
color: var(--text-primary, #FFFFFF)
```

## Testing Checklist
- [x] Build succeeds
- [ ] Genre filter text is readable (white on dark gray)
- [ ] Cluster filter text is readable
- [ ] Search input text is visible
- [ ] Modal header text is readable (black on green)
- [ ] Modal body text is readable (white on dark)
- [ ] Table headers are readable (gray on dark)
- [ ] Table cells are readable (white on dark)
- [ ] Hover states maintain contrast
- [ ] Focus states are visible

## Before/After

**Before:**
- White text on white background (invisible)
- Bootstrap light theme defaults
- Poor contrast throughout

**After:**
- Consistent Spotify dark theme
- High contrast text (white on dark, black on green)
- Brand-consistent styling
- Accessible color contrast ratios
