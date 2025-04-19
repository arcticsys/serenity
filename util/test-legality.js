const fs = require('fs');
const http = require('http');
const filename = process.argv[2];

if (!filename) {
    console.error('No filename provided');
    console.log('Usage: bun run test-legality.js <filename>');
    process.exit(1);
}

fs.readFile(filename, 'binary', (err, data) => {
    if (err) {
        console.error('Error reading file:', err);
        return;
    }

    const base64Content = Buffer.from(data, 'binary').toString('base64');

    const jsonData = JSON.stringify({
        pkmdata: base64Content
    });

    const options = {
        hostname: 'localhost',
        port: 5008,
        path: '/api/islegal',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': jsonData.length
        },
        rejectUnauthorized: false
    };

    const req = http.request(options, (res) => {
        let responseData = '';

        res.on('data', (chunk) => {
            responseData += chunk;
        });

        res.on('end', () => {
            let jsonResponse;
            try {
                jsonResponse = JSON.parse(responseData);
                fs.writeFile('./legality-response', JSON.stringify(jsonResponse, null, 4), (err) => {
                    if (err) {
                        console.error('Error writing response to file:', err);
                        return;
                    }
                    console.log('Written response');
                    console.log(jsonResponse);
                });
            } catch (parseErr) {
                console.error('Whoopsie daisies, looks like the response couldn\'t be parsed for some reason :+1:\nError:', parseErr);
                fs.writeFile('./legality-response', responseData, (err) => {
                    if (err) {
                    console.error('Error writing response to file:', err);
                    }
                });
            }
        });
    });

    req.on('error', (error) => {
        console.error('Error sending POST request:', error);
    });

    req.write(jsonData);
    req.end();
});