# Repo Working Rules

## File Links In Responses

- If you need to give the user a clickable vault file link, use this exact pattern:
  `[relative/path/from/vault.md](</absolute/path/to/file.md>)`
- Link text must be the path relative to the vault root.
- `href` must be the full absolute path.
- If the path contains spaces, keep the absolute path wrapped in `<...>`.
- Do not switch to URL-encoded targets with `%20` or `%D0...`.
- Do not use bare `D:/...` in `href` when a clickable link is needed.
- Do not invent alternate link formats.

Example:

```md
[05 Уровень квестов/03 Структуры и давление/01 Узлы давления квеста.md](</D:/codex/quests/docs/obsidian/05 Уровень квестов/03 Структуры и давление/01 Узлы давления квеста.md>)
```

## File Writes In This Repo

- In this repo, always write files through PowerShell.
- Do not rely on `apply_patch`, `Set-Content`, or other write paths for repo files in this environment.
- Use PowerShell with `[System.IO.File]::ReadAllText/WriteAllText` and explicit `UTF-8 BOM`.
- This is a hard rule because other write methods have repeatedly broken links, Unicode text, or sandbox refresh.
- After noticeable vault edits, run `python tools/validate-vault.py`.

Required pattern:

```powershell
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$text = [System.IO.File]::ReadAllText($path)
$updated = $text.Replace($old, $new)
[System.IO.File]::WriteAllText($path, $updated, $utf8Bom)
```

## Tags In This Vault

- Every note in `docs/obsidian` must have at least one primary kind tag.
- Allowed primary kind tags are: `root`, `moc`, `concept`, `reference`, `template`, `guide`, `example`, `entry`.
- Order tags as: primary kind first, then working tags, then domain tags.
