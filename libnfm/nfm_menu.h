#ifndef NFM_MENU_H
#define NFM_MENU_H

#include <stdint.h>
#include <stddef.h>
#include <windows.h>

typedef void (*nfm_on_select_callback)(HWND hwnd, void* state);
typedef void (*nfm_on_closed_callback)(void);
typedef void (*nfm_on_select_string_callback)(char* message, void* state);
typedef char** (*nfm_items_action_callback)(void* state);

typedef void (*nfm_initialize_func)();
typedef void (*nfm_show_file_system_func)(
    nfm_on_select_string_callback onSelect,
    nfm_on_closed_callback onClosed,
    void* state
);
typedef void (*nfm_show_programs_list_func)(
    nfm_on_select_string_callback onSelect,
    nfm_on_closed_callback onClosed,
    void* state
);
typedef void (*nfm_show_windows_list_func)(
    nfm_on_select_callback onSelect,
    nfm_on_closed_callback onClosed,
    void* state
);
typedef void (*nfm_show_processes_list_func)(
    nfm_on_select_string_callback onSelect,
    nfm_on_closed_callback onClosed,
    void* state
);
typedef void (*nfm_show_items_list_func)(
    nfm_items_action_callback nativeItemsAction,
    nfm_on_select_string_callback onSelect,
    nfm_on_closed_callback onClosed,
    void* state
);
typedef void (*nfm_hide_func)();

HMODULE nfm_load_library(const char* dllPath);
void nfm_unload_library(HMODULE hModule);

extern nfm_initialize_func nfm_initialize;
extern nfm_show_file_system_func nfm_show_file_system;
extern nfm_show_programs_list_func nfm_show_programs_list;
extern nfm_show_windows_list_func nfm_show_windows_list;
extern nfm_show_processes_list_func nfm_show_processes_list;
extern nfm_show_items_list_func nfm_show_items_list;
extern nfm_hide_func nfm_hide;

#endif
