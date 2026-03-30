/*
 * native_pty.c — Native helper for fork+exec in a PTY.
 *
 * .NET 10's runtime cannot safely execute managed code (including
 * P/Invoke marshaling) after fork().  This small C helper performs
 * forkpty + execve entirely in native code so the child process
 * never touches the managed runtime.
 *
 * Build (macOS):  cc -shared -o libaerovim_pty.dylib native_pty.c
 * Build (Linux):  cc -shared -fPIC -o libaerovim_pty.so native_pty.c -lutil
 *
 * Copyright (c) aerovim Developers. All rights reserved.
 * Licensed under the GPLv2 license.
 */

#if defined(__APPLE__)
#include <util.h>
#elif defined(__linux__)
#include <pty.h>
#endif

#include <unistd.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
#include <signal.h>
#include <sys/wait.h>

/*
 * spawn_in_pty — Create a PTY and spawn a process inside it.
 *
 * Parameters:
 *   app       — Absolute path to the executable.
 *   argv      — NULL-terminated argument array (argv[0] = app).
 *   envp      — NULL-terminated environment array ("KEY=VAL"), or
 *               NULL to inherit the parent's environment.
 *   cwd       — Working directory for the child, or NULL to inherit.
 *   rows      — Initial terminal row count.
 *   cols      — Initial terminal column count.
 *   master_fd — [out] Receives the master-side file descriptor.
 *
 * Returns the child PID on success, or -1 on error (errno is set).
 */
int spawn_in_pty(const char *app,
                 char *const argv[],
                 char *const envp[],
                 const char *cwd,
                 unsigned short rows,
                 unsigned short cols,
                 int *master_fd)
{
    struct winsize ws;
    memset(&ws, 0, sizeof(ws));
    ws.ws_row = rows;
    ws.ws_col = cols;

    int master = -1;
    pid_t pid = forkpty(&master, NULL, NULL, &ws);

    if (pid == -1)
    {
        /* forkpty failed */
        return -1;
    }

    if (pid == 0)
    {
        /* ---- Child process (pure C, no managed runtime) ---- */

        /* Reset signal dispositions to defaults */
        struct sigaction sa;
        memset(&sa, 0, sizeof(sa));
        sa.sa_handler = SIG_DFL;
        for (int sig = 1; sig < 32; sig++)
            sigaction(sig, &sa, NULL);

        if (cwd != NULL && cwd[0] != '\0')
        {
            if (chdir(cwd) != 0)
            {
                _exit(127);
            }
        }

        if (envp != NULL)
        {
            execve(app, argv, envp);
        }
        else
        {
            execv(app, argv);
        }

        /* exec failed */
        _exit(errno);
    }

    /* ---- Parent process ---- */
    *master_fd = master;
    return (int)pid;
}

/*
 * wait_for_exit — Non-blocking check on whether a child has exited.
 *
 * Returns 1 if the child exited (status filled in), 0 if still
 * running, or -1 on error.
 */
int wait_for_exit(int pid, int *exit_code, int *exit_signal)
{
    int status = 0;
    pid_t w = waitpid(pid, &status, WNOHANG);
    if (w == 0)
    {
        return 0; /* still running */
    }
    if (w == -1)
    {
        return -1; /* error */
    }

    if (WIFEXITED(status))
    {
        *exit_code   = WEXITSTATUS(status);
        *exit_signal = 0;
    }
    else if (WIFSIGNALED(status))
    {
        *exit_code   = 0;
        *exit_signal = WTERMSIG(status);
    }
    else
    {
        *exit_code   = -1;
        *exit_signal = 0;
    }
    return 1; /* exited */
}
