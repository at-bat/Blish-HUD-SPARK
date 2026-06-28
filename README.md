# SPARK: Simple Profile and Roleplay Kit

SPARK is a [Blish HUD](https://blishhud.com) module for Guild Wars 2 roleplayers. It lets players create character profiles, share their current roleplay status, discover other SPARK users, and save notes or bookmarks for profiles they have viewed.

## Features

- Multiple profiles for each character
- RP Statuses (Online, Looking for RP, Busy, Invisible (appear offline))
- 'At a Glance' icons with custom tooltips including a fast, built-in icon search with over 57,000 icons from the game to use
- Bookmarks for your favourite RP profiles
- Private notes that you can save to any profile you view
- Privacy options to hide your current location or appear offline
- Filter/sorting for profiles on the online list, recently seen, or bookmarked lists
- Block option (Account-wide) to block all profiles from someone. This prevents blocked users from seeing you online or fetching your profile anymore
- Report option to report offensive profiles or users who promote hate or bigotry.
- Mature Profile opt-in / filtering

## Installing SPARK

1. Close Blish HUD if it's currently running.
2. Go to the [Release Page](https://github.com/at-bat/Blish-HUD-SPARK/releases). Download the `.bhm` file and place it in your Blish HUD directory.
	On Windows this is typically here: `Documents\Guild Wars 2\addons\blishhud\modules`.
3. Once it's added to this folder, start Guild Wars 2 and Blish HUD and you should see SPARK in the Blish HUD sidebar.

SPARK contains a server status and a module status to let you know if something isn't working to troubleshoot problems.

**Please make sure you have an API key added into Blish HUD, or SPARK will not work.**

API keys can only read your account data, like your account name, character names, etc. It cannot modify or change anything on your account and are safe to put into Blish HUD.

## Requirements

- [Blish HUD](https://blishhud.com) 1.3.0 or newer
- Guild Wars 2 API key (added to your Blish HUD) with account and characters permissions.

SPARK uses these permissions to verify account and character ownership and tie profiles to your GW2 account.

This is done to prevent potential impersonation or abuse issues and provide SPARK with the ability to restrict malicious users from using the module.

## Privacy / Data

To share your RP profile with other players, this module relays your data through `spark.a-bat.com`.

- **Strictly for syncing**: Your data is only used to send your profile to others.
- **Automatic Deletion**: If you do not appear online for 24 hours, your profile data is automatically removed from the server.
- **No tracking or AI**: SPARK does not use profile data for analytics, machine learning, or AI tools. Reported profiles may be reviewed only for moderation purposes.

When you view someone's profile, you save a copy of their profile to your PC.

### What data is shared?

SPARK only publishes your profile and presence data if you enable profile sharing. 

Presence is a mini-snippet of your profile, used for tooltips when hovering over a profile on the online list and populating a few fields.

**Profile Data**:

- GW2 Account Name
- Character Name
- Display Name (if different from character name)
- Profile ID (to know which one this is tied to on your characters if you have several and swap between them)
- Profile Name (the name you specifically put for the profile)
- RP Status (Online, Looking, Busy, Offline, etc.)
- Pronouns (if set)
- Character Race
- Character Profession
- Custom Profession (if set)
- Region (for filtering between NA and EU profiles)
- Current Location (if online and not set to be hidden)
- 'Currently' status (if set)
- Out of Character Info (if set)
- At a Glance Icons and custom tooltips/descriptions (if set)
- RP Experience (if set)
- RP Preferences (if set)
- RP Themes (if set)
- RP Styles (if set)
- 'Known for' info (a small RP hook you can optionally add)
- 'Description' info (your main profile box)

**Presence Data:**

- GW2 Account Name
- Character Name
- Display Name (if different from character name)
- RP Status (Online, Looking, Busy, Offline, etc.)
- 'Currently' status (if set)
- Out of Character Info (if set)
- Current Location (if online and not set to be hidden)
- Character Race
- Character Profession
- Custom Profession (if set)

**Account Blocks:**

When you block an account, this is in a locally kept list on the module for players to manage. Block information is sent to the server in order to prevent a blocked user from seeing the person who blocked them as online, their location, or any profile updates.

Blocked users may have already viewed a profile and have a local copy on their machine. This is the only profile they will be able to view and will never see any updates or anything else from that GW2 Account unless unblocked.

### Reported Profiles / SPARK Moderation

SPARK retains a snapshot of a profile when it has been reported by users. Report reasons are appended to the profile, so if multiple people report a profile, their report messages are added into a server-only copy of the profile for review.

These snapshots are retained for moderation review purposes only. They are not exposed through the module API.

If the profile appears to be breaking any TOS or promotes hate/bigotry, they will be added to a ban list on the server. This prevents any uploads from their GW2 account to SPARK permanently until removed from the ban list.

If a SPARK user includes explicit information in a profile without marking it as 18+, SPARK may force all of that account's profiles to be marked as 18+ indefinitely.

## Installation

Eventually, this will be done through Blish HUD itself. For now, if you're looking to test this, see below:

Place the ``.bhm`` file from the Releases page in your Blish HUD module folder. On Windows this is typically `Documents\Guild Wars 2\addons\blishhud\modules`.

Once it's added, start Guild Wars 2 and Blish HUD and you should see it in the Blish HUD sidebar.

## LICENSE

This project is licensed under the GNU General Public License v3.0 - see the [COPYING](COPYING) file for license details.