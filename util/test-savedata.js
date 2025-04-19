const fs = require('fs');
const http = require('http');

fs.readFile('./main', 'binary', (err, data) => {
    if (err) {
        console.error('Error reading file:', err);
        return;
    }

    const base64Content = Buffer.from(data, 'binary').toString('base64');

    const jsonData = JSON.stringify({
        savedata: base64Content
    });

    const options = {
        hostname: 'localhost',
        port: 5008,
        path: '/api/savedata',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': jsonData.length
        },
        rejectUnauthorized: false
    };

    fs.readdir('.', (err, files) => {
        if (err) {
            console.error('Error reading directory:', err);
            return;
        }

        console.log('Clearing directory of .pk* files...');

        files.forEach(file => {
            if (file.includes('.pk')) {
                fs.unlink(file, (err) => {
                    if (err) {
                        console.error(`Error clearing directory (${file}):`, err);
                    }
                });
            }
        });
    });

    const req = http.request(options, (res) => {
        let responseData = '';

        res.on('data', (chunk) => {
            responseData += chunk;
        });

        res.on('end', () => {
            fs.writeFile('./savedata-response', responseData, (err) => {
                if (err) {
                    console.error('Error writing response to file:', err);
                    return;
                }
                console.log('Written response');

                let jsonResponse;
                try {
                    jsonResponse = JSON.parse(responseData);
                } catch (parseErr) {
                    console.error('Whoopsie daisies, looks like the response couldn\'t be parsed for some reason :+1:\nError:', parseErr);
                    return;
                }

                if (jsonResponse) {
                    const { PartyData, BoxData } = jsonResponse;
                    const writtenFiles = new Set();

                    const saveDataToFile = (dataArray, dataType) => {
                        dataArray.forEach((data, index) => {
                            const fileName = data.FileName;
                            if (writtenFiles.has(fileName)) {
                                console.warn(`Duplicate PKM: ${fileName}`);
                            } else {
                                writtenFiles.add(fileName);
                                const decodedData = Buffer.from(data.Data, 'base64');
                                fs.writeFile(fileName, decodedData, (err) => {
                                    if (err) {
                                        console.error(`Error with PKM (${fileName}):`, err);
                                    } else {
                                        console.log(`${dataType} Index ${index} - ${fileName} written successfully`);
                                    }
                                });
                            }
                        });
                    };
                    if (PartyData) { saveDataToFile(PartyData, 'PartyData'); }
                    if (BoxData) { saveDataToFile(BoxData, 'BoxData'); }
                } else {
                    console.error('Blocks object not found in response');
                }
            });
        });
    });

    req.on('error', (error) => {
        console.error('Error sending POST request:', error);
    });

    req.write(jsonData);
    req.end();
});