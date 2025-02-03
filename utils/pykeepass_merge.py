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
                target_entry.title = "" if source_entry.title is None else source_entry.title
                target_entry.username = "" if source_entry.username is None else source_entry.username
                target_entry.password = "" if source_entry.password is None else source_entry.password
                target_entry.url = "" if source_entry.url is None else source_entry.url
                target_entry.notes = "" if source_entry.notes is None else source_entry.notes
                target_entry.icon = "" if source_entry.icon is None else source_entry.icon
                target_entry.tags = "" if source_entry.tags is None else source_entry.tags
                target_entry.otp = "" if source_entry.otp is None else source_entry.otp
                try: 
                    target_entry.autotype_enabled = source_entry.autotype_enabled 
                except Exception as e:
                    pass
                try:
                    target_entry.autotype_sequence = source_entry.autotype_sequence
                except Exception as e:
                    pass
                try:
                    target_entry.autotype_window = source_entry.autotype_window
                except Exception as e:
                    pass
                
                target_entry.history.extend(source_entry.history)
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
