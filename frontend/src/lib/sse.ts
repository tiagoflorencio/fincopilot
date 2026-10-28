/**
 * Parser incremental de Server-Sent Events. O EventSource nativo só faz GET, então
 * lemos o corpo de um POST via fetch e montamos os eventos à medida que os chunks chegam.
 * Um chunk pode trazer meio evento ou vários; o resto fica no buffer até o próximo.
 */
export function createSseParser(onData: (data: string) => void) {
  let buffer = ''

  return (chunk: string) => {
    buffer += chunk.replace(/\r\n/g, '\n')

    let boundary: number
    while ((boundary = buffer.indexOf('\n\n')) !== -1) {
      const rawEvent = buffer.slice(0, boundary)
      buffer = buffer.slice(boundary + 2)

      const data = rawEvent
        .split('\n')
        .filter((line) => line.startsWith('data:'))
        .map((line) => line.slice(5).trimStart())
        .join('\n')

      if (data) onData(data)
    }
  }
}
