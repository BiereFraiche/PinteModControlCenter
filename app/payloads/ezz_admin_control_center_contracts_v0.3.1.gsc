// ============================================================
// PinteMod — Control Center Contracts Preview v0.3.1
// Fichier : ezz_admin_control_center_contracts.gsc
//
// Bridge minimal pour PinteMod v2.1.1 + Control Center Preview.
// - publie capabilities + identité serveur structurées ;
// - hostname public via live_steam_server_name (+ sv_hostname compat) ;
// - net_password observé uniquement comme booléen ;
// - SET password disponible uniquement si le Control Center l'envoie
//   (le client impose loopback avant transport) ;
// - Change Map fermé et limité à une allowlist locale explicite.
// - Boss PinteMod fermés par carte avec feedback structuré.
//
// Aucun secret, IP, XUID ou chemin privé n'est écrit par ce module.
// Tous les fichiers sont relatifs à boiii/scriptdata/.
// ============================================================

#using custom_scripts\ezz_admin_storage;
#using custom_scripts\ezz_admin_identity;

function cc_is_digit(character)
{
    return character == "0" || character == "1" ||
        character == "2" || character == "3" ||
        character == "4" || character == "5" ||
        character == "6" || character == "7" ||
        character == "8" || character == "9";
}

function cc_is_alpha(character)
{
    c = toLower(character);

    return c == "a" || c == "b" || c == "c" || c == "d" ||
        c == "e" || c == "f" || c == "g" || c == "h" ||
        c == "i" || c == "j" || c == "k" || c == "l" ||
        c == "m" || c == "n" || c == "o" || c == "p" ||
        c == "q" || c == "r" || c == "s" || c == "t" ||
        c == "u" || c == "v" || c == "w" || c == "x" ||
        c == "y" || c == "z";
}

function cc_is_alnum(character)
{
    return cc_is_alpha(character) || cc_is_digit(character);
}

function cc_is_lower_hex(character)
{
    return cc_is_digit(character) || character == "a" ||
        character == "b" || character == "c" ||
        character == "d" || character == "e" ||
        character == "f";
}

function cc_is_request_id(value)
{
    if (!isdefined(value) || value.size < 8 || value.size > 32)
        return false;

    for (i = 0; i < value.size; i++)
    {
        c = GetSubStr(value, i, i + 1);

        if (!cc_is_alnum(c) && c != "_" && c != "-")
            return false;
    }

    return true;
}

function cc_is_map_request_id(value)
{
    if (!isdefined(value) || value.size != 32)
        return false;

    for (i = 0; i < value.size; i++)
    {
        c = GetSubStr(value, i, i + 1);

        if (!cc_is_lower_hex(c))
            return false;
    }

    return true;
}

function cc_is_safe_hostname_character(character)
{
    if (cc_is_alnum(character))
        return true;

    return character == " " || character == "-" ||
        character == "_" || character == "." ||
        character == "[" || character == "]" ||
        character == "(" || character == ")" ||
        character == "|";
}


function cc_is_xuid(value)
{
    if (!isdefined(value) || value.size != 16)
        return false;

    lower = toLower(value);

    for (i = 0; i < lower.size; i++)
    {
        c = GetSubStr(lower, i, i + 1);

        if (!cc_is_lower_hex(c))
            return false;
    }

    return true;
}

function cc_events_available()
{
    if (!isdefined(level.pintemod_events_loaded) ||
        !level.pintemod_events_loaded)
    {
        return false;
    }

    if (isdefined(level.pintemod_enable_events) &&
        !level.pintemod_enable_events)
    {
        return false;
    }

    return true;
}

function cc_boss_alias_count_for_map(map_code)
{
    if (!cc_events_available())
        return 0;

    switch (map_code)
    {
        case "zm_zod": return 1;
        case "zm_castle": return 1;
        case "zm_island": return 1;
        case "zm_stalingrad": return 1;
        case "zm_genesis": return 4;
        case "zm_moon": return 1;
        case "zm_tomb": return 1;
    }

    return 0;
}

function cc_boss_alias_for_map(map_code, index)
{
    switch (map_code)
    {
        case "zm_zod":
            if (index == 1) return "margwa";
            break;
        case "zm_castle":
            if (index == 1) return "panzer";
            break;
        case "zm_island":
            if (index == 1) return "thrasher";
            break;
        case "zm_stalingrad":
            if (index == 1) return "panzer";
            break;
        case "zm_genesis":
            if (index == 1) return "margwa";
            if (index == 2) return "shadow_margwa";
            if (index == 3) return "fire_margwa";
            if (index == 4) return "panzer";
            break;
        case "zm_moon":
            if (index == 1) return "astronaut";
            break;
        case "zm_tomb":
            if (index == 1) return "panzer";
            break;
    }

    return "";
}

function cc_boss_spawner_for(map_code, alias)
{
    if (map_code == "zm_zod" && alias == "margwa")
        return "spawner_zm_zod_margwa";

    if (map_code == "zm_castle" && alias == "panzer")
        return "spawner_zm_castle_mechz";

    if (map_code == "zm_island" && alias == "thrasher")
        return "spawner_zm_island_thrasher";

    if (map_code == "zm_stalingrad" && alias == "panzer")
        return "spawner_zm_stalingrad_mechz";

    if (map_code == "zm_genesis")
    {
        if (alias == "margwa")
            return "spawner_zm_genesis_margwa";
        if (alias == "shadow_margwa")
            return "spawner_zm_genesis_margwa_shadow";
        if (alias == "fire_margwa")
            return "spawner_zm_genesis_margwa_fire";
        if (alias == "panzer")
            return "spawner_zm_genesis_mechz";
    }

    if (map_code == "zm_moon" && alias == "astronaut")
        return "spawner_zm_moon_astro";

    if (map_code == "zm_tomb" && alias == "panzer")
        return "spawner_zm_tomb_mechz";

    return "";
}

function cc_count_active_pintemod_bosses()
{
    alive = [];
    count = 0;

    if (!isdefined(level.pintemod_spawned_bosses))
    {
        level.pintemod_spawned_bosses = [];
        return 0;
    }

    for (i = 0; i < level.pintemod_spawned_bosses.size; i++)
    {
        actor = level.pintemod_spawned_bosses[i];

        if (!isdefined(actor) || !IsAlive(actor))
            continue;

        alive[alive.size] = actor;
        count++;
    }

    level.pintemod_spawned_bosses = alive;
    return count;
}

function cc_max_pintemod_bosses()
{
    if (isdefined(level.pintemod_max_spawned_bosses))
        return int(level.pintemod_max_spawned_bosses);

    return 2;
}

function cc_boss_get_aim_position(player)
{
    eye = player GetEye();
    angles = player GetPlayerAngles();
    forward = AnglesToForward(angles);
    trace_end = eye + (forward * 8192);
    aim_trace = BulletTrace(eye, trace_end, false, player);

    if (aim_trace["fraction"] >= 1)
        return undefined;

    hit_position = aim_trace["position"];
    floor_start = hit_position + (0, 0, 128);
    floor_end = hit_position + (0, 0, -768);
    floor_trace = BulletTrace(floor_start, floor_end, false, player);

    if (floor_trace["fraction"] < 1)
        return floor_trace["position"] + (0, 0, 8);

    return hit_position + (0, 0, 8);
}

function cc_boss_position_is_safe(player, position)
{
    if (!isdefined(position))
        return false;

    return Distance(player.origin, position) >= 200;
}

function cc_mark_gameplay_command(command_name, target_name)
{
    level.pintemod_gameplay_command_pending = true;
    level.pintemod_gameplay_command_name = command_name;
    level.pintemod_gameplay_command_target = target_name;
    level notify("pintemod_gameplay_command_used", command_name, target_name);
}

function cc_register_boss(actor)
{
    if (!isdefined(actor))
        return;

    if (!isdefined(level.pintemod_spawned_bosses))
        level.pintemod_spawned_bosses = [];

    level.pintemod_spawned_bosses[level.pintemod_spawned_bosses.size] = actor;
}

function cc_is_valid_hostname(value)
{
    if (!isdefined(value) || value.size < 1 || value.size > 64)
        return false;

    if (GetSubStr(value, 0, 1) == " " ||
        GetSubStr(value, value.size - 1, value.size) == " ")
    {
        return false;
    }

    for (i = 0; i < value.size; i++)
    {
        c = GetSubStr(value, i, i + 1);

        if (c == "^")
        {
            if (i + 1 >= value.size)
                return false;

            next = GetSubStr(value, i + 1, i + 2);
            if (!cc_is_digit(next))
                return false;

            i++;
            continue;
        }

        if (!cc_is_safe_hostname_character(c))
            return false;
    }

    return true;
}

function cc_neutralize_observed_hostname(value)
{
    if (!isdefined(value) || value == "")
        return "";

    result = "";
    limit = value.size;

    if (limit > 96)
        limit = 96;

    for (i = 0; i < limit; i++)
    {
        c = GetSubStr(value, i, i + 1);

        if (c == "^")
        {
            if (i + 1 < limit)
            {
                next = GetSubStr(value, i + 1, i + 2);
                if (cc_is_digit(next))
                {
                    result = result + c + next;
                    i++;
                    continue;
                }
            }

            result = result + "_";
            continue;
        }

        if (cc_is_safe_hostname_character(c))
            result = result + c;
        else
            result = result + "_";
    }

    return result;
}

function cc_hostname_state(raw_value, published_value)
{
    if (!isdefined(raw_value) || raw_value == "")
        return "empty";

    if (raw_value == published_value)
        return "observed";

    return "neutralized";
}

function cc_is_valid_join_password(value)
{
    if (!isdefined(value) || value.size < 4 || value.size > 32)
        return false;

    for (i = 0; i < value.size; i++)
    {
        c = GetSubStr(value, i, i + 1);

        if (cc_is_alnum(c))
            continue;

        if (c != "-" && c != "_" && c != "." &&
            c != "!" && c != "@" && c != "#" &&
            c != "$" && c != "%" && c != "+")
        {
            return false;
        }
    }

    return true;
}

function cc_join_args(args, start_index)
{
    result = "";

    for (i = start_index; i < args.size; i++)
    {
        if (result != "")
            result = result + " ";

        result = result + args[i];
    }

    return result;
}

function cc_is_official_map(map_code)
{
    return map_code == "zm_zod" ||
        map_code == "zm_castle" ||
        map_code == "zm_island" ||
        map_code == "zm_stalingrad" ||
        map_code == "zm_genesis" ||
        map_code == "zm_cosmodrome" ||
        map_code == "zm_theater" ||
        map_code == "zm_moon" ||
        map_code == "zm_prototype" ||
        map_code == "zm_tomb" ||
        map_code == "zm_temple" ||
        map_code == "zm_sumpf" ||
        map_code == "zm_factory" ||
        map_code == "zm_asylum";
}

function cc_allowlist_load()
{
    path = "pintemod/config/control_center_map_allowlist.json";

    if (!fileexists(path))
        return undefined;

    json = readfile(path);

    if (!ezz_admin_storage::storage_json_is_valid(json))
        return undefined;

    schema = jsonparse(json, "schema_version");
    authority = jsonparse(json, "authority");
    count_value = jsonparse(json, "count");

    if (!isdefined(schema) || int(schema) != 1 ||
        !isdefined(authority) || authority != "operator_declared" ||
        !isdefined(count_value))
    {
        return undefined;
    }

    count = int(count_value);
    if (count < 0 || count > 14)
        return undefined;

    result = SpawnStruct();
    result.json = json;
    result.count = count;
    return result;
}

function cc_allowlist_contains(map_code)
{
    allowlist = cc_allowlist_load();

    if (!isdefined(allowlist))
        return false;

    for (i = 1; i <= allowlist.count; i++)
    {
        candidate = jsonparse(allowlist.json, "map_" + i);

        if (!isdefined(candidate) || !cc_is_official_map(candidate))
            return false;

        if (candidate == map_code)
            return true;
    }

    return false;
}

function cc_allowlist_available()
{
    allowlist = cc_allowlist_load();

    if (!isdefined(allowlist) || allowlist.count <= 0)
        return false;

    for (i = 1; i <= allowlist.count; i++)
    {
        candidate = jsonparse(allowlist.json, "map_" + i);

        if (!isdefined(candidate) || !cc_is_official_map(candidate))
            return false;
    }

    return true;
}

function cc_current_public_hostname_raw()
{
    value = GetDvarString("live_steam_server_name");

    if (!isdefined(value) || value == "")
        value = GetDvarString("sv_hostname");

    if (!isdefined(value))
        return "";

    return "" + value;
}

function cc_join_password_enabled()
{
    value = GetDvarString("net_password");
    return isdefined(value) && value != "";
}

function cc_apply_hostname(value)
{
    SetDvar("live_steam_server_name", value);
    SetDvar("sv_hostname", value);
}

function cc_identity_config_load_hostname()
{
    path = "pintemod/config/control_center_identity.json";

    if (!fileexists(path))
        return "";

    json = readfile(path);

    if (!ezz_admin_storage::storage_json_is_valid(json))
        return "";

    schema = jsonparse(json, "schema_version");
    hostname = jsonparse(json, "public_hostname");

    if (!isdefined(schema) || int(schema) != 1 ||
        !isdefined(hostname) || !cc_is_valid_hostname(hostname))
    {
        return "";
    }

    return hostname;
}

function cc_identity_config_save_hostname(hostname)
{
    json = "{}";
    json = jsonset(json, "schema_version", "1");
    json = jsonset(json, "public_hostname", hostname);
    json = jsonset(json, "updated_gettime", "" + GetTime());

    return ezz_admin_storage::write_json_safe(
        "pintemod/config/control_center_identity.json",
        json,
        "control-center-identity-config"
    );
}

function cc_feedback_write(request_id, action, status, result_code)
{
    level.pintemod_cc_feedback_sequence++;

    json = "{}";
    json = jsonset(json, "schema_version", "1");
    json = jsonset(json, "session_id", ezz_admin_storage::get_session_id());
    json = jsonset(json, "sequence", "" + level.pintemod_cc_feedback_sequence);
    json = jsonset(json, "generated_gettime", "" + GetTime());
    json = jsonset(json, "updated_at_utc", "");
    json = jsonset(json, "time_authority", "session_gettime_and_file_mtime");
    json = jsonset(json, "request_id", request_id);
    json = jsonset(json, "action", action);
    json = jsonset(json, "status", status);
    json = jsonset(json, "result_code", result_code);

    return ezz_admin_storage::write_json_safe(
        "pintemod/remote/action_feedback.latest.json",
        json,
        "control-center-feedback"
    );
}

function cc_identity_refresh_revision(public_hostname, join_enabled)
{
    if (!isdefined(level.pintemod_cc_identity_revision))
        level.pintemod_cc_identity_revision = 1;

    if (!isdefined(level.pintemod_cc_last_identity_hostname))
    {
        level.pintemod_cc_last_identity_hostname = public_hostname;
        level.pintemod_cc_last_identity_password = join_enabled;
        return;
    }

    if (level.pintemod_cc_last_identity_hostname != public_hostname ||
        level.pintemod_cc_last_identity_password != join_enabled)
    {
        level.pintemod_cc_identity_revision++;
        level.pintemod_cc_last_identity_hostname = public_hostname;
        level.pintemod_cc_last_identity_password = join_enabled;
    }
}

function cc_publish_identity()
{
    raw_hostname = cc_current_public_hostname_raw();
    public_hostname = cc_neutralize_observed_hostname(raw_hostname);
    hostname_state = cc_hostname_state(raw_hostname, public_hostname);
    join_enabled = cc_join_password_enabled();

    cc_identity_refresh_revision(public_hostname, join_enabled);
    level.pintemod_cc_identity_sequence++;

    json = "{}";
    json = jsonset(json, "schema_version", "1");
    json = jsonset(json, "session_id", ezz_admin_storage::get_session_id());
    json = jsonset(json, "sequence", "" + level.pintemod_cc_identity_sequence);
    json = jsonset(json, "generated_gettime", "" + GetTime());
    json = jsonset(json, "updated_at_utc", "");
    json = jsonset(json, "time_authority", "session_gettime_and_file_mtime");
    json = jsonset(json, "public_hostname", public_hostname);
    json = jsonset(json, "public_hostname_state", hostname_state);

    if (join_enabled)
        json = jsonset(json, "join_password_enabled", "true");
    else
        json = jsonset(json, "join_password_enabled", "false");

    json = jsonset(json, "revision", "" + level.pintemod_cc_identity_revision);

    return ezz_admin_storage::write_json_safe(
        "pintemod/runtime/server_identity.json",
        json,
        "control-center-server-identity"
    );
}

function cc_publish_capabilities()
{
    level.pintemod_cc_capabilities_sequence++;
    map_code = GetDvarString("mapname");

    if (!isdefined(map_code) || !cc_is_official_map(map_code))
        map_code = "zm_unknown";

    map_profile = "operator_allowlist_unavailable";
    if (cc_allowlist_available())
        map_profile = "operator_allowlist";

    json = "{}";
    json = jsonset(json, "schema_version", "1");
    json = jsonset(json, "module_version", "2.1.1");
    json = jsonset(json, "contract_module_version", "0.3.1");
    json = jsonset(json, "command_contract_version", "1");
    json = jsonset(json, "session_id", ezz_admin_storage::get_session_id());
    json = jsonset(json, "sequence", "" + level.pintemod_cc_capabilities_sequence);
    json = jsonset(json, "generated_gettime", "" + GetTime());
    json = jsonset(json, "updated_at_utc", "");
    json = jsonset(json, "time_authority", "session_gettime_and_file_mtime");
    json = jsonset(json, "map_code", map_code);
    json = jsonset(json, "map_source", "runtime");
    json = jsonset(json, "map_installation_authority", "unknown");
    json = jsonset(json, "map_count", "0");
    json = jsonset(json, "rotation_state", "unknown");
    json = jsonset(json, "rotation_entry_count", "0");
    json = jsonset(json, "change_map", "false");
    json = jsonset(json, "restart_map", "false");
    boss_count = cc_boss_alias_count_for_map(map_code);
    json = jsonset(json, "event_count", "0");
    json = jsonset(json, "boss_count", "" + boss_count);
    for (boss_index = 1; boss_index <= boss_count; boss_index++)
    {
        json = jsonset(
            json,
            "boss_" + boss_index + "_alias",
            cc_boss_alias_for_map(map_code, boss_index)
        );
    }
    json = jsonset(json, "power_up_count", "0");
    if (cc_events_available())
    {
        json = jsonset(json, "diagnostic_count", "1");
        json = jsonset(json, "diagnostic_1_alias", "event_status");
    }
    else
    {
        json = jsonset(json, "diagnostic_count", "0");
    }
    json = jsonset(json, "transition_state", "idle");
    json = jsonset(json, "set_hostname", "true");
    json = jsonset(json, "set_join_password", "true");
    json = jsonset(json, "clear_join_password", "true");
    json = jsonset(json, "join_password_transport", "loopback_rcon_ephemeral");
    json = jsonset(json, "map_profile", map_profile);
    json = jsonset(json, "power_support", "unknown");
    json = jsonset(json, "pack_a_punch_support", "unknown");
    if (cc_events_available())
        json = jsonset(json, "event_support", "pintemod_events_loaded");
    else
        json = jsonset(json, "event_support", "unavailable");

    if (boss_count > 0)
        json = jsonset(json, "boss_support", "closed_aliases_on_active_map");
    else if (cc_events_available())
        json = jsonset(json, "boss_support", "none_on_active_map");
    else
        json = jsonset(json, "boss_support", "unavailable");
    json = jsonset(json, "music_support", "unknown");
    json = jsonset(json, "dog_round_support", "unknown");
    if (cc_events_available())
    {
        json = jsonset(json, "active_pintemod_bosses", "" + cc_count_active_pintemod_bosses());
        json = jsonset(json, "max_pintemod_bosses", "" + cc_max_pintemod_bosses());
    }
    else
    {
        json = jsonset(json, "active_pintemod_bosses", "0");
        json = jsonset(json, "max_pintemod_bosses", "0");
    }

    return ezz_admin_storage::write_json_safe(
        "pintemod/diagnostics/control_center_capabilities.json",
        json,
        "control-center-capabilities"
    );
}

function cc_publish_all()
{
    cc_publish_capabilities();
    cc_publish_identity();
}

function cc_contract_monitor()
{
    for (;;)
    {
        cc_publish_all();
        wait 2;
    }
}

function cc_request_is_duplicate(request_id)
{
    if (!isdefined(level.pintemod_cc_last_request_id))
        return false;

    return level.pintemod_cc_last_request_id == request_id;
}

function cc_mark_request(request_id)
{
    level.pintemod_cc_last_request_id = request_id;
}

function cmd_ezzccsethostname(args)
{
    if (args.size < 2 || !cc_is_request_id(args[0]))
    {
        println("^1[PinteMod CC]^7 HOSTNAME_REJECTED | code=invalid_request_id");
        return;
    }

    request_id = args[0];

    if (cc_request_is_duplicate(request_id))
    {
        cc_feedback_write(request_id, "set_hostname", "rejected", "duplicate_request");
        println("^3[PinteMod CC]^7 HOSTNAME_REJECTED | code=duplicate_request");
        return;
    }

    cc_mark_request(request_id);
    hostname = cc_join_args(args, 1);

    if (!cc_is_valid_hostname(hostname))
    {
        cc_feedback_write(request_id, "set_hostname", "rejected", "invalid_hostname");
        println("^3[PinteMod CC]^7 HOSTNAME_REJECTED | code=invalid_hostname");
        return;
    }

    cc_apply_hostname(hostname);

    if (!cc_identity_config_save_hostname(hostname))
    {
        cc_feedback_write(request_id, "set_hostname", "failed", "hostname_persist_failed");
        cc_publish_identity();
        println("^1[PinteMod CC]^7 HOSTNAME_FAILED | code=hostname_persist_failed");
        return;
    }

    observed = cc_current_public_hostname_raw();
    cc_publish_identity();

    if (observed != hostname)
    {
        cc_feedback_write(request_id, "set_hostname", "failed", "hostname_not_applied");
        println("^1[PinteMod CC]^7 HOSTNAME_FAILED | code=hostname_not_applied");
        return;
    }

    cc_feedback_write(request_id, "set_hostname", "applied", "success");
    println("^2[PinteMod CC]^7 HOSTNAME_APPLIED");
}

function cmd_ezzccsetjoinpassword(args)
{
    if (args.size != 2 || !cc_is_request_id(args[0]))
    {
        println("^1[PinteMod CC]^7 PASSWORD_REJECTED | code=invalid_request_id");
        return;
    }

    request_id = args[0];

    if (cc_request_is_duplicate(request_id))
    {
        cc_feedback_write(request_id, "set_join_password", "rejected", "duplicate_request");
        println("^3[PinteMod CC]^7 PASSWORD_REJECTED | code=duplicate_request");
        return;
    }

    cc_mark_request(request_id);

    if (!cc_is_valid_join_password(args[1]))
    {
        cc_feedback_write(request_id, "set_join_password", "rejected", "invalid_arguments");
        println("^3[PinteMod CC]^7 PASSWORD_REJECTED | code=invalid_arguments");
        return;
    }

    // Ne jamais journaliser ni republier la valeur.
    SetDvar("net_password", args[1]);
    cc_publish_identity();
    cc_feedback_write(request_id, "set_join_password", "applied", "success");
    println("^2[PinteMod CC]^7 PASSWORD_APPLIED");
}

function cmd_ezzccclearjoinpassword(args)
{
    if (args.size != 1 || !cc_is_request_id(args[0]))
    {
        println("^1[PinteMod CC]^7 PASSWORD_CLEAR_REJECTED | code=invalid_request_id");
        return;
    }

    request_id = args[0];

    if (cc_request_is_duplicate(request_id))
    {
        cc_feedback_write(request_id, "clear_join_password", "rejected", "duplicate_request");
        println("^3[PinteMod CC]^7 PASSWORD_CLEAR_REJECTED | code=duplicate_request");
        return;
    }

    cc_mark_request(request_id);
    SetDvar("net_password", "");
    cc_publish_identity();
    cc_feedback_write(request_id, "clear_join_password", "applied", "success");
    println("^2[PinteMod CC]^7 PASSWORD_CLEARED");
}


function cmd_ezzccboss(args)
{
    if (args.size != 3 || !cc_is_request_id(args[0]))
    {
        println("^1[PinteMod CC]^7 BOSS_REJECTED | code=invalid_request_id");
        return;
    }

    request_id = args[0];
    alias = toLower(args[1]);
    target_xuid = toLower(args[2]);

    if (cc_request_is_duplicate(request_id))
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "duplicate_request");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=duplicate_request");
        return;
    }

    cc_mark_request(request_id);

    if (!cc_is_xuid(target_xuid))
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "invalid_target_xuid");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=invalid_target_xuid");
        return;
    }

    if (!cc_events_available())
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "events_disabled");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=events_disabled");
        return;
    }

    map_code = toLower(GetDvarString("mapname"));
    spawner = cc_boss_spawner_for(map_code, alias);
    if (spawner == "")
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "unsupported_on_map");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=unsupported_on_map");
        return;
    }

    max_bosses = cc_max_pintemod_bosses();
    active_bosses = cc_count_active_pintemod_bosses();
    if (max_bosses > 0 && active_bosses >= max_bosses)
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "boss_limit_reached");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=boss_limit_reached");
        return;
    }

    player = ezz_admin_identity::identity_find_player(target_xuid);
    if (!isdefined(player))
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "target_not_connected");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=target_not_connected");
        return;
    }

    spawn_position = cc_boss_get_aim_position(player);
    if (!cc_boss_position_is_safe(player, spawn_position))
    {
        cc_feedback_write(request_id, "spawn_boss", "rejected", "invalid_position");
        println("^3[PinteMod CC]^7 BOSS_REJECTED | code=invalid_position");
        return;
    }

    player_angles = player GetPlayerAngles();
    spawn_angles = (0, player_angles[1], 0);
    actor = SpawnActor(
        spawner,
        spawn_position,
        spawn_angles,
        undefined,
        true,
        true
    );

    if (!isdefined(actor))
    {
        cc_feedback_write(request_id, "spawn_boss", "failed", "spawn_failed");
        println("^1[PinteMod CC]^7 BOSS_FAILED | code=spawn_failed");
        return;
    }

    cc_mark_gameplay_command("control center spawn boss", player.name);
    cc_register_boss(actor);
    actor.pintemod_spawned_event = true;
    actor.pintemod_event_type = alias;

    cc_publish_capabilities();
    cc_feedback_write(request_id, "spawn_boss", "applied", "success");
    println("^2[PinteMod CC]^7 BOSS_APPLIED | alias=" + alias);
}

function cc_apply_map(map_code)
{
    wait 0.35;
    ExecuteCommand("map " + map_code);
}

function cmd_ezzccmap(args)
{
    if (args.size != 2 || !cc_is_map_request_id(args[0]))
    {
        println("^1[PinteMod CC]^7 CHANGE_MAP_REJECTED | code=invalid_request_id");
        return;
    }

    request_id = args[0];
    map_code = toLower(args[1]);

    if (!cc_is_official_map(map_code))
    {
        println("^3[PinteMod CC]^7 CHANGE_MAP_REJECTED | code=map_not_allowed");
        return;
    }

    // supported != installed. L'autorité v0.2 est l'allowlist locale
    // explicitement déclarée par l'opérateur via le BAT d'installation.
    if (!cc_allowlist_contains(map_code))
    {
        println("^3[PinteMod CC]^7 CHANGE_MAP_REJECTED | code=map_not_allowed");
        return;
    }

    if (isdefined(level.pintemod_cc_map_transition_pending) &&
        level.pintemod_cc_map_transition_pending)
    {
        println("^3[PinteMod CC]^7 CHANGE_MAP_REJECTED | code=transition_in_progress");
        return;
    }

    level.pintemod_cc_map_transition_pending = true;
    println("^2[PinteMod CC]^7 CHANGE_MAP_ACCEPTED | map=" + map_code);
    level thread cc_apply_map(map_code);
}

autoexec function init()
{
    if (isdefined(level.pintemod_cc_contracts_loaded) &&
        level.pintemod_cc_contracts_loaded)
    {
        return;
    }

    level.pintemod_cc_contracts_loaded = true;
    level.pintemod_cc_capabilities_sequence = 0;
    level.pintemod_cc_identity_sequence = 0;
    level.pintemod_cc_feedback_sequence = 0;
    level.pintemod_cc_identity_revision = 1;
    level.pintemod_cc_map_transition_pending = false;
    level.pintemod_cc_last_request_id = undefined;
    level.pintemod_cc_last_identity_hostname = undefined;
    level.pintemod_cc_last_identity_password = undefined;

    mkdir("pintemod");
    mkdir("pintemod/config");
    mkdir("pintemod/diagnostics");
    mkdir("pintemod/runtime");
    mkdir("pintemod/remote");

    persisted_hostname = cc_identity_config_load_hostname();
    if (persisted_hostname != "")
        cc_apply_hostname(persisted_hostname);

    addcommand("ezzccsethostname", ::cmd_ezzccsethostname);
    addcommand("ezzccsetjoinpassword", ::cmd_ezzccsetjoinpassword);
    addcommand("ezzccclearjoinpassword", ::cmd_ezzccclearjoinpassword);
    addcommand("ezzccmap", ::cmd_ezzccmap);
    addcommand("ezzccboss", ::cmd_ezzccboss);

    cc_publish_all();
    level thread cc_contract_monitor();

    println("^5[PinteMod]^7 Control Center Contracts Preview v0.3.1 loaded");
}
