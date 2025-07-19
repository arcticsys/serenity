const fs = require('fs');
const path = require('path');
const http = require('http');

const SERVER_OPTIONS = {
    hostname: 'localhost',
    port: 5008,
    path: '/api/savedata',
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
    },
    rejectUnauthorized: false
};

let totalSaves = 0;
let goodSaves = 0;

function walkdir(dir, fileList = []) {
    const files = fs.readdirSync(dir);
    for (const file of files) {
        const fullPath = path.join(dir, file);
        const stat = fs.statSync(fullPath);
        if (stat.isDirectory()) {
            walkdir(fullPath, fileList);
        } else {
            fileList.push(fullPath);
        }
    }
    return fileList;
}

function csf(filePath) {
    return new Promise((resolve) => {
        fs.readFile(filePath, 'binary', (err, data) => {
            if (err) {
                console.error(`[READ ERROR] ${filePath}:`, err);
                return resolve(false);
            }

            const base64Content = Buffer.from(data, 'binary').toString('base64');
            const jsonData = JSON.stringify({ savedata: base64Content });

            const req = http.request(
                { ...SERVER_OPTIONS, headers: { ...SERVER_OPTIONS.headers, 'Content-Length': Buffer.byteLength(jsonData) } },
                (res) => {
                    let responseData = '';
                    res.on('data', chunk => responseData += chunk);
                    res.on('end', () => {
                        try {
                            const json = JSON.parse(responseData);
                            if (json.error) {
                                console.warn(`[INVALID SAVE] ${filePath} → ${json.error}`);
                                resolve(false);
                            } else {
                                resolve(true);
                            }
                        } catch (parseErr) {
                            console.error(`[PARSE ERROR] ${filePath}:`, parseErr.message);
                            resolve(false);
                        }
                    });
                }
            );

            req.on('error', (error) => {
                console.error(`[HTTP ERROR] ${filePath}:`, error.message);
                resolve(false);
            });

            req.write(jsonData);
            req.end();
        });
    });
}

async function saveinteg(rootDir) {
    const files = walkdir(rootDir);
    await Promise.all(files.map(async file => {
        const ok = await csf(file);
        totalSaves++;
        if (ok) goodSaves++;
    }));
    console.log(`\nCompleted: ${goodSaves}/${totalSaves} saves passed integrity check.`);
}

saveinteg('./0x01B51 Pokémon Ultra Moon');