"""
This script updates the changelog with the commit message.
"""


def update_changelog():
    """Updates the changelog with the commit message."""
    with open("commit_message.txt", "r", encoding='utf-8') as commit_file:
        commit_message = commit_file.read().strip()

    changelog_file = "CHANGELOG.md"
    with open(changelog_file, "a", encoding='utf-8') as changelog:
        changelog.write(f"\n- {commit_message}")


if __name__ == "__main__":
    update_changelog()
