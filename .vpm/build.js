import fs from 'node:fs/promises';
import path from 'node:path';
import url from 'node:url';
import crypto from 'node:crypto';
import timers from 'node:timers/promises';
import os from 'node:os';
import core from '@actions/core';
import semver from 'semver';
import tar from 'tar';
import AdmZip from 'adm-zip';
import yaml from 'js-yaml';
import * as openupmEnv from 'openupm-cli/lib/utils/env.js';
import * as openupmRegistryClient from 'openupm-cli/lib/registry-client.js';

/** VPMパッケージ化する最初のバージョン。 */
const MIN_VERSION = '40.0.1';
const INTERVAL_MILISECONDS = 1 * 60 * 1000;
const IGNORE_PACKAGE_NAME_PREFIX = 'com.vrchat.';
const IGNORE_PACKSGE_NAME_FROM_VPM_DEPENDENCIES_PREFIX = 'com.unity.';

const vpmDirectoryPath = path.dirname(url.fileURLToPath(import.meta.url));
const { name } = JSON.parse(await fs.readFile(path.join(vpmDirectoryPath, '..', 'package.json')));
const env = await openupmEnv.parseEnv({ _global: { } });
const npmClient = openupmRegistryClient.getNpmClient();
if (!process.env.GITHUB_ACTIONS) {
	// ローカルデバッグ
	process.env.TAG_NAME
		= 'v' + (await openupmRegistryClient.fetchPackument(env.registry, name, npmClient))['dist-tags'].latest;
	process.env.GITHUB_REPOSITORY = 'esperecyan/VRMConverterForVRChat';
}
const latestVersion = process.env.TAG_NAME.replace('v', '');

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
const { packages } = registry;

const dependencies = [ ];
const registeredVersions = Object.keys(packages[name]?.versions ?? { });
for (const version of new Set(
	Object.keys((await openupmRegistryClient.fetchPackument(env.registry, name, npmClient)).versions)
		.concat([ latestVersion ]),
)) {
	if (semver.lt(version, MIN_VERSION) || registeredVersions.includes(version)) {
		// VPMパッケージ化する最初のバージョンより小さいバージョン (VPMパッケージ化しないバージョン)
		// またはすでにレジストリへ追加済みのバージョンなら
		continue;
	}

	while (true) {
		let [ validDependencies, invalidDependencies ] = await openupmRegistryClient
			.fetchPackageDependencies(env.registry, env.upstreamRegistry, name, version, true, npmClient);
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

		dependencies.push(...validDependencies);
		break;
	}
}

// パッケージを保存するフォルダを作成
const packagesDirectoryPath = path.join(registryDirectoryPath, 'packages');
try {
	await fs.mkdir(packagesDirectoryPath);
} catch (exception) {
	// フォルダが存在する場合
}

const [ owner, repositoryName ] = process.env.GITHUB_REPOSITORY.split('/');
const packageURLPrefix = `https://${owner}.github.io/${repositoryName}/packages/`;

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
			await (await fetch(
				(await openupmRegistryClient.fetchPackument(env.registry, name, npmClient))
					.versions[version].dist.tarball,
			)).arrayBuffer(),
		));
		await tar.extract({ file: tarFilePath, cwd: extractedDirectoryPath, strip: 1 });

		const manifestPath = path.join(extractedDirectoryPath, 'package.json');
		manifest = JSON.parse(await fs.readFile(manifestPath));
		if (manifest.dependencies) {
			manifest.vpmDependencies = Object.fromEntries(Object.entries(manifest.dependencies)
				.filter(([ name ]) => !name.startsWith(IGNORE_PACKSGE_NAME_FROM_VPM_DEPENDENCIES_PREFIX))
				.map(([ name, version ]) => [
					name,
					(manifest.name.startsWith('com.vrmc.') && name.startsWith('com.vrmc.') ? '' : '^') + version,
				]));
		}
		Object.assign(manifest, namePartialManifestPairs[name]);
		if (manifest.name === 'com.vrmc.gltf' && semver.gte(manifest.version, '0.125.0')) {
			// UniGLTF-0.125.0かそれ以上のバージョンなら
			// VRMShardersが統合されているため
			manifest.legacyPackages = (manifest.legacyPackages ?? [ ]).concat([ 'com.vrmc.vrmshaders' ]);
			Object.assign(manifest.legacyFolders, namePartialManifestPairs['com.vrmc.vrmshaders'].legacyFolders);
		}
		manifest.url = packageURLPrefix + packageFileName;
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
