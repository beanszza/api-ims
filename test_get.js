async function test() {
    try {
        const res = await fetch('http://localhost:5001/api/Items', {
            method: 'GET'
        });
        const text = await res.text();
        console.log("SUCCESS:", res.status, text.substring(0, 50));
    } catch(e) {
        console.log("ERROR:", e.message);
    }
}
test();
