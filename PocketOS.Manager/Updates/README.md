# PocketOS Update Manifest Migration

Purpose:
Remove dependency on GitHub API calls.

New flow:

PocketOS Manager
        |
        v
pocketos-manifest.json
        |
        v
Release assets

Benefits:
- Avoid GitHub API rate limits
- Manual update checks supported
- Daily automatic checks supported
