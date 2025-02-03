def merge_databases(source_db, target_db):
    """
    Merge the contents of the source KeePass database into the target KeePass database.

    Args:
        source_db (PyKeePass): The source database to merge from.
        target_db (PyKeePass): The target database to merge into.
    """
    # Merge groups recursively
    def merge_groups(source_group, target_group):
        for source_subgroup in source_group.subgroups:
            # Check if the subgroup already exists in the target group
            target_subgroup = target_db.find_groups(
                name=source_subgroup.name,
                group=target_group,
                first=True,
                recursive=False
            )
            if not target_subgroup:
                # Create the subgroup if it doesn't exist
                target_subgroup = target_db.add_group(
                    target_group,
                    source_subgroup.name,
                    icon=source_subgroup.icon,
                    notes=source_subgroup.notes
                )
            # Recursively merge subgroups
            merge_groups(source_subgroup, target_subgroup)

        # Merge entries in the current group
        for source_entry in source_group.entries:
            target_entry = target_db.find_entries(
                uuid=source_entry.uuid,
                group=target_group,
                first=True,
                recursive=False
            )
            if target_entry:
                # Handle conflict (e.g., overwrite or skip)
                print(f"Conflict detected for entry: {source_entry.title}")
                # Example: Overwrite the target entry with the source entry
                target_entry.title = source_entry.title
                target_entry.username = source_entry.title
                target_entry.password = source_entry.title
                target_entry.url = source_entry.title
                target_entry.notes = source_entry.title
                target_entry.icon = source_entry.title
                target_entry.tags = source_entry.title
                target_entry.otp = source_entry.title
                target_entry.history = source_entry.title
                target_entry.autotype_enabled = source_entry.title
                target_entry.autotype_sequence = source_entry.title
                target_entry.autotype_window = source_entry.title
            else:
                # Add the entry if it doesn't exist
                """
                    def add_entry(self, destination_group, title, username,
                                password, url=None, notes=None, expiry_time=None,
                                tags=None, otp=None, icon=None, force_creation=False):
                """                
                target_db.add_entry(
                    target_group,
                    title = source_entry.title,
                    username = source_entry.username,
                    password = source_entry.password,
                    url = source_entry.url,
                    notes = source_entry.notes,
                    icon = source_entry.icon,
                    tags = source_entry.tags,
                    otp = source_entry.otp,
                    force_creation=True
                )

    # Start merging from the root groups
    merge_groups(source_db.root_group, target_db.root_group)
