import { execFile } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { delimiter, join } from 'node:path';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

type BuildExitCode = 0 | 1;

const repositories: string[] = [];

export const createRepository = async (
  packages: Record<string, BuildExitCode> = {},
) => {
  const root = await mkdtemp(join(tmpdir(), 'agent-check-'));
  repositories.push(root);

  await execFileAsync('git', ['init', '--quiet'], { cwd: root });

  for (const [path, buildExitCode] of Object.entries(packages)) {
    const directory = join(root, path);
    await writeCheck(root, path, 'testClasses', buildExitCode);
    await writeGradleWrapper(directory, 'exec node check.cjs "$@"');
  }

  return root;
};

export const addDotnetPackage = async (
  root: string,
  path: string,
  buildExitCode: BuildExitCode,
) => {
  await writeCheck(root, path, 'build', buildExitCode);
  await writeFile(join(root, path, 'Package.slnx'), '<Solution />');
  await writeExecutable(join(root, 'fake-bin', 'dotnet'), 'exec node check.cjs "$@"');
};

const writeCheck = async (
  root: string,
  path: string,
  compileTask: string,
  buildExitCode: BuildExitCode,
) => {
  const directory = join(root, path);
  await mkdir(join(directory, 'src'), { recursive: true });
  await writeFile(
    join(directory, 'check.cjs'),
    [
      `const task = process.argv[2];`,
      `require('node:fs').appendFileSync(${JSON.stringify(join(root, 'runs'))}, ${JSON.stringify(path)} + ' ' + task + '\\n');`,
      `console.error(task + ' output in ' + process.cwd());`,
      `process.exit(task === ${JSON.stringify(compileTask)} ? ${buildExitCode} : 0);`,
    ].join('\n'),
  );
};

const writeExecutable = async (file: string, script: string) => {
  await mkdir(join(file, '..'), { recursive: true });
  await writeFile(file, `#!/bin/sh\n${script}\n`);
  await chmod(file, 0o755);
};

export const writeGradleWrapper = (directory: string, script: string) =>
  writeExecutable(join(directory, 'gradlew'), script);

export const checkRuns = async (root: string) => {
  try {
    return (await readFile(join(root, 'runs'), 'utf8'))
      .split('\n')
      .filter(Boolean);
  } catch {
    return [];
  }
};

export const removeRepositories = () =>
  Promise.all(
    repositories.map((root) => rm(root, { recursive: true, force: true })),
  );

export type HookRun = { exitCode: number; stdout: string; stderr: string };

export const runHook = (script: string, input: unknown, root?: string) =>
  new Promise<HookRun>((resolve) => {
    const env = root
      ? { ...process.env, PATH: [join(root, 'fake-bin'), process.env.PATH].join(delimiter) }
      : process.env;
    const child = execFile(
      process.execPath,
      [script],
      { env },
      (error, stdout, stderr) =>
        resolve({
          exitCode: typeof error?.code === 'number' ? error.code : 0,
          stdout,
          stderr,
        }),
    );
    child.stdin?.end(JSON.stringify(input));
  });
