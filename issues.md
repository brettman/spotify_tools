# Known Issues & Minor Bugs

This file tracks minor issues and UX improvements to address later.

## Issues

### 1. Edit Cluster - Can't Exit Without Changes
**Severity:** Minor
**Component:** Cluster Management UI
**Description:** When editing a saved cluster, the user is presented with a genre removal interface but cannot explicitly cancel/exit without making changes. The only way to exit is to not select any genres and press Enter, which is not intuitive.

**Expected Behavior:** Add a "← Cancel / Go Back" option in the genre removal interface when editing a cluster.

**Location:** `CliMenuService.cs` - `EditClusterAsync()` method, specifically in the `RemoveGenresFromClusterAsync()` call.

**Suggested Fix:** Modify `RemoveGenresFromClusterAsync()` to detect when it's being called from edit mode and add a cancel option, or create a separate edit-specific genre removal method.

---

### 2. Cluster Selection Menu - Markup Escaping Error ✅ FIXED
**Severity:** High (causes crash)
**Component:** Saved Clusters UI
**Description:** After viewing cluster details and trying to return to the cluster list, the app crashes with "Unbalanced markup stack. Did you forget to close a tag?" error. This happens because cluster IDs are displayed in square brackets (e.g., `[2]`) which Spectre.Console interprets as markup tags.

**Status:** ✅ **FIXED** - Changed format from `[ID] Name` to `#ID - Name` and added `.EscapeMarkup()` for cluster names.

**Location:** `CliMenuService.cs` - `ViewSavedClustersAsync()` method, line 875

**Fix Applied:**
```csharp
var clusterChoices = savedClusters
    .Select(c => $"#{c.Id} - {c.Name.EscapeMarkup()} ({c.TotalTracks} tracks, {c.Genres.Count} genres)")
```

---

### 3. Cluster Suggestions Include Already-Organized Genres
**Severity:** Medium (UX/Confusion)
**Component:** Cluster Generation
**Description:** When generating new cluster suggestions, the system includes genres that have already been organized into saved clusters or explicitly removed by the user. This creates confusion as users see the same genres in multiple suggested clusters, or see genres they've already decided to exclude.

**Example Scenario:**
1. User creates "Metal & Heavy" cluster with genres: doom metal, stoner metal, sludge metal
2. User removes genres: thrash metal, death metal, black metal (sends to "Unclustered")
3. User generates new cluster suggestions
4. System suggests clusters containing doom/stoner/sludge metal (already saved) AND thrash/death/black metal (already removed)

**Expected Behavior:**
- Genres already in saved clusters should be excluded from new suggestions
- Genres explicitly removed/unclustered should be excluded from new suggestions
- Only unorganized genres should appear in new cluster suggestions

**Location:** `AnalyticsService.cs` - `SuggestGenreClustersAsync()` method

**Suggested Fix:**
1. Before generating suggestions, query all genres from saved clusters
2. Track "unclustered" genres (requires new table or cluster with special flag)
3. Filter these genres out of the suggestion algorithm
4. Optionally: Add a "Show all genres" toggle to bypass this filter

**Implementation Notes:**
- May need to track "unclustered" genres separately (new table or special cluster)
- Consider adding a refresh/regenerate option that ignores saved clusters
- UI could show: "X genres excluded (already organized)"

---

### 4. Track Browsing - Row Selection Not Visually Clear
**Severity:** Minor (UX/Clarity)
**Component:** Track Viewing UI
**Description:** When browsing tracks in a saved cluster, the option to select a row (by typing 1-30) is not visually distinct from the letter-based commands. The current format `[N]ext [P]rev [J]ump [1-30] Select row [E]dit mode [B]ack` makes it hard to understand that you can select a row by number.

**Current Format:**
```
[cyan]Options:[/] [green][[N]]ext[/] [green][[P]]rev[/] [yellow][[J]]ump[/] [yellow][[1-30]][/] Select row [cyan][[E]]dit mode[/] [dim][[B]]ack[/]
```

**Expected Behavior:** Make the row selection option more prominent and separated from letter-based navigation commands.

**Location:** `CliMenuService.cs` - `ViewClusterTracksAsync()` method, line ~1198

**Suggested Fix Options:**
1. Put row selection first: `[1-30] Select row | [N]ext [P]rev [J]ump [E]dit [B]ack`
2. Use different formatting: `Type [green bold]1-30[/] to select track | [N]ext [P]rev [J]ump [E]dit [B]ack`
3. Add separator with distinct color: `[yellow bold][[1-30]] Select row[/] | [green][[N]]ext[/] ...`
4. Break into two lines:
   ```
   Navigation: [N]ext [P]rev [J]ump [E]dit [B]ack
   Select track: Type 1-30
   ```

**Implementation Notes:**
- Same issue may exist in other paginated views (artists, playlists)
- Consider standardizing navigation prompts across all paginated views
- Could also apply to the artist browsing view for consistency

---

*Last Updated: 2026-01-03*
