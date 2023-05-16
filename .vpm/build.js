import fs from 'node:fs/promises';
import path from 'node:path';
import url from 'node:url';
import crypto from 'node:crypto';
import timers from 'node:timers/promises';
import os from 'node:os';
import core from '@actions/core';
import tar from 'tar';
import AdmZip from 'adm-zip';
import yaml from 'js-yaml';
import openupm from 'openupm-cli/lib/core.js';

const INTERVAL_MILISECONDS = 1 * 60 * 1000;
const IGNORE_PACKAGE_NAME_PREFIX = 'com.vrchat.';

const vpmDirectoryPath = path.dirname(url.fileURLToPath(import.meta.url));
const { name } = JSON.parse(await fs.readFile(path.join(vpmDirectoryPath, '..', 'package.json')));
if (!process.env.GITHUB_ACTIONS) {
	// ローカルデバッグ
	process.env.TAG_NAME = 'v39.0.0';
}
const version = process.env.TAG_NAME.replace('v', '');

await openupm.parseEnv({ _global: { } }, { });

let dependencies;
while (true) {
	let invalidDependencies;
	[ dependencies, invalidDependencies ] = await openupm.fetchPackageDependencies({ name, version, deep: true });
	invalidDependencies = invalidDependencies.filter(({ name }) => !name.startsWith(IGNORE_PACKAGE_NAME_PREFIX));

	const package404Dependencies = invalidDependencies.filter(({ reason }) => reason === 'package404');
	if (package404Dependencies.length > 0) {
		throw new DOMException('次のパッケージはOpenUPMレジストリに存在しません:\n'
			+ package404Dependencies.map(({ name }) => name).join('\n'), 'NotFoundError');
	}

	if (invalidDependencies.length > 0) {
		core.debug(`次のパッケージのバージョンはOpenUPMレジストリに存在しないため、${INTERVAL_MILISECONDS} ミリ秒待機:\n`
			+ invalidDependencies.map(({ name, version }) => name + '@' + version).join('\n'));
		await timers.setTimeout(INTERVAL_MILISECONDS);
		continue;
	}

	break;
}

// レジストリを保存するフォルダを作成
const registryDirectoryPath = path.join(vpmDirectoryPath, 'registry');
try {
	await fs.mkdir(registryDirectoryPath);
} catch (exception) {
	// フォルダが存在する場合
}

// レジストリのキャッシュの読み込み
const registryPath = path.join(registryDirectoryPath, 'registry.json');
let registry;
try {
	registry = JSON.parse(await fs.readFile(registryPath));
} catch (exception) {
	// キャッシュが存在しない場合
	registry = yaml.load(await fs.readFile(path.join(vpmDirectoryPath, 'registry-template.yaml')));
}

// パッケージを保存するフォルダを作成
const packagesDirectoryPath = path.join(vpmDirectoryPath, 'packages');
try {
	await fs.mkdir(packagesDirectoryPath);
} catch (exception) {
	// フォルダが存在する場合
}

const { packages } = registry;
const namePartialManifestPairs = yaml.load(await fs.readFile(path.join(vpmDirectoryPath, 'partial-manifests.yaml')));
for (const { name, version, internal } of dependencies) {
	if (internal) {
		continue;
	}

	if (!packages[name]) {
		packages[name] = { versions: { } };
	}

	if (packages[name].versions[version]) {
		continue;
	}

	core.notice(`${name}@${version} をレジストリへ追加します。`);
	const packageFileName = `${name}-${version}.zip`;
	let manifest, zip;
	const tarDirectoryPath = await fs.mkdtemp(os.tmpdir() + '/');
	const extractedDirectoryPath = await fs.mkdtemp(os.tmpdir() + '/');
	try {
		const tarFilePath = path.join(tarDirectoryPath, 'package.tar.gz');
		await fs.writeFile(tarFilePath, Buffer.from(
			await (await fetch((await openupm.fetchPackageInfo(name)).versions[version].dist.tarball)).arrayBuffer(),
		));
		await tar.extract({ file: tarFilePath, cwd: extractedDirectoryPath, strip: 1 });

		const manifestPath = path.join(extractedDirectoryPath, 'package.json');
		manifest = JSON.parse(await fs.readFile(manifestPath));
		manifest.vpmDependencies = manifest.dependencies;
		Object.assign(manifest, namePartialManifestPairs[name]);
		manifest.url = `https://github.com/${process.env.GITHUB_REPOSITORY}/releases/download/${process.env.TAG_NAME}/${packageFileName}`; //eslint-disable-line max-len
		await fs.writeFile(manifestPath, JSON.stringify(manifest, null, '\t'));

		zip = new AdmZip();
		await zip.addLocalFolderPromise(extractedDirectoryPath);
	} finally {
		await fs.rm(tarDirectoryPath, { recursive: true });
		await fs.rm(extractedDirectoryPath, { recursive: true });
	}
	const packageFileBuffer = await zip.toBufferPromise();
	manifest.zipSHA256 = crypto.createHash('sha256').update(packageFileBuffer).digest('hex');
	await fs.writeFile(path.join(packagesDirectoryPath, packageFileName), packageFileBuffer);

	packages[name].versions[version] = manifest;
}

// レジストリの保存
await fs.writeFile(registryPath, JSON.stringify(registry, null, '\t'));
