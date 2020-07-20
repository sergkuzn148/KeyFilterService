module ShapeFileReader

open System
open System.IO
open System.Buffers.Binary

type FileHeader = {
    Code: Int32; 
    Length: Int32;
    Version: Int32;
    ShapeType: Int32;
    XMin: double;
    YMin: double;
    XMax: double;
    YMax: double;    
    ZMin: double;
    ZMax: double;    
    MMin: double;
    MMax: double;
}   

let readHeader file =          
    use rd = new BinaryReader(File.OpenRead file)
    let buf = rd.ReadBytes 100
    rd.Close()
    let fc = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[0..4]))
    let len = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[24..28]))
    let ver = BitConverter.ToInt32(buf, 28)
    let typ = BitConverter.ToInt32(buf, 32)
    let xmin = BitConverter.ToDouble(buf, 36)
    let ymin = BitConverter.ToDouble(buf, 44)
    let xmax = BitConverter.ToDouble(buf, 52)
    let ymax = BitConverter.ToDouble(buf, 60)
    let zmin = BitConverter.ToDouble(buf, 68)
    let zmax = BitConverter.ToDouble(buf, 76)
    let mmin = BitConverter.ToDouble(buf, 84)
    let mmax = BitConverter.ToDouble(buf, 92)    

    { 
        Code = fc;
        Length = len;
        Version = ver;
        ShapeType = typ;
        XMin = xmin;
        YMin = ymin;
        XMax = xmax;
        YMax = ymax;
        ZMin = zmin;
        ZMax = zmax;
        MMin = mmin;
        MMax = mmax;
    }

[<AbstractClass>]
type Shape () =
    abstract member ToGeoJson: unit -> string

type NullShape () =
    inherit Shape()
    override this.ToGeoJson () = ""

type Point (x,y) =
    inherit Shape()
    member this.X with get () = x
    member this.Y with get () = y
    override this.ToGeoJson () = sprintf "{\"type\": \"Point\", \"coordinates\": [%f, %f]}" x y

let private readPoint (buf:byte[]) offset =
    let x = BitConverter.ToDouble(buf, offset)
    let y = BitConverter.ToDouble(buf, offset + 8)
    Point (x, y)

type Polyline (xmin:double, ymin:double, xmax:double, ymax:double, numParts:int32, numPoints:int32, parts:int32[], points:Point[]) =
    inherit Shape()
    member this.XMin with get() = xmin
    member this.YMin with get() = ymin
    member this.XMax with get() = xmax
    member this.YMax with get() = ymax
    member this.NumParts with get() = numParts
    member this.NumPoints with get() = numPoints
    member this.Parts with get() = parts
    member this.Points with get() = points
    override this.ToGeoJson () =
        points
        |> Array.map(fun p -> sprintf "[%f, %f]" p.X p.Y)
        |> String.concat ","
        |> sprintf "{\"type\": \"LineString\", \"coordinates\": [%s]}"

let private readPolyline (buf:byte[]) =    
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)
    
    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)

    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    Polyline(xmin, ymin, xmax, ymax, numParts, numPoints, parts, points)

type Polygon (xmin:double, ymin:double, xmax:double, ymax:double, numParts:int32, numPoints:int32, parts:int32[], points:Point[]) =
    inherit Polyline(xmin, ymin, xmax, ymax, numParts, numPoints, parts, points)
    override this.ToGeoJson () =
        [|
            let mutable p = parts.[0]
            for n in Array.skip 1 parts do
                yield p, n - 1
                p <- n
            yield p, points.Length - 1
        |]
        |> Array.map(fun (a,b) ->
            points.[a .. b]
            |> Array.map (fun p -> sprintf "[%f, %f]" p.X p.Y)
            |> String.concat ","
        )
        |> Array.map (sprintf "[%s]")
        |> String.concat ","
        |> sprintf "{\"type\": \"Polygon\", \"coordinates\": [%s]}"

let private readPolygon (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)
    
    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)

    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    Polygon(xmin, ymin, xmax, ymax, numParts, numPoints, parts, points)

type MultiPoint (xmin:double, ymin:double, xmax:double, ymax:double, numPoints:int32, points:Point[]) =
    inherit Shape ()
    member this.XMin with get() = xmin
    member this.YMin with get() = ymin
    member this.XMax with get() = xmax
    member this.YMax with get() = ymax
    member this.NumPoints with get() = numPoints    
    member this.Points with get() = points
    override this.ToGeoJson () =
        points
        |> Array.map (fun p -> sprintf "[%f, %f]" p.X p.Y)
        |> String.concat ","
        |> sprintf "{\"type\": \"MultiPoint\", \"coordinates\": [%s]}"

let private readMultiPoint (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numPoints = BitConverter.ToInt32(buf, 36)
    let mutable x = 40
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]  
    MultiPoint(xmin, ymin, xmax, ymax, numPoints, points)

type PointM (x:double, y:double, m:double) =
    inherit Point(x, y)    
    member this.M with get () = m

let private readPointm (buf:byte[]) =
    let x = BitConverter.ToDouble(buf, 4)
    let y = BitConverter.ToDouble(buf, 12)
    let m = BitConverter.ToDouble(buf, 20)
    PointM (x, y, m)

type PolylineM (xmin:double, ymin:double, xmax:double, ymax:double, numParts, numPoints, parts, points, mmin:double, mmax:double, marray:double[]) =
    inherit Polyline (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points)
    member this.MMin with get() = mmin
    member this.MMax with get() = mmax
    member this.MArray with get() = marray

let private readPolylineM (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)
    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    
    let mmin = BitConverter.ToDouble(buf, x)
    let mmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let marray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    PolylineM (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, mmin, mmax, marray)

type PolygonM (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, mmin, mmax, marray) =
    inherit PolylineM (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, mmin, mmax, marray)

let private readPolygonM (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)
    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    
    let mmin = BitConverter.ToDouble(buf, x)
    let mmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let marray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    PolygonM (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, mmin, mmax, marray)

type MultiPointM (xmin, ymin, xmax, ymax, numPoints, points, mmin:double, mmax:double, marray:double[]) =
    inherit MultiPoint (xmin, ymin, xmax, ymax, numPoints, points)
    member this.MMin with get() = mmin
    member this.MMax with get() = mmax
    member this.MArray with get() = marray

let private readMultiPointM (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numPoints = BitConverter.ToInt32(buf, 36)
    let mutable x = 40
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    let mmin = if points.Length > 0 then BitConverter.ToDouble(buf, x) else 0.0
    let mmax = if points.Length > 0 then BitConverter.ToDouble(buf, x + 8) else 0.0
    let marray = [|
        if points.Length > 0 then        
            x <- x + 16
            for i = 1 to numPoints do
                yield BitConverter.ToDouble(buf, x)
                x <- x + 8
    |]
    MultiPointM(xmin, ymin, xmax, ymax, numPoints, points, mmin, mmax, marray)

type PointZ (x, y, z:double, m) =
    inherit PointM (x, y, m)
    member this.Z with get () = z

let private readPointZ (buf:byte[]) =
    let x = BitConverter.ToDouble(buf, 4)
    let y = BitConverter.ToDouble(buf, 12)
    let z = BitConverter.ToDouble(buf, 20)
    let m = BitConverter.ToDouble(buf, 28)
    PointZ (x, y, z, m)

type MultiPointZ (xmin, ymin, xmax, ymax, numPoints, points, zmin:double, zmax:double, zarray:double[], mmin, mmax, marray) =
    inherit MultiPointM (xmin, ymin, xmax, ymax, numPoints, points, mmin, mmax, marray)
    member this.ZMin with get() = zmin
    member this.ZMax with get() = zmax
    member this.ZArray with get() = zarray

let private readMultiPointZ (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numPoints = BitConverter.ToInt32(buf, 36)
    let mutable x = 40
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]
    let zmin = BitConverter.ToDouble(buf, x)
    let zmax = BitConverter.ToDouble(buf, x + 8)
    x <- x + 16
    let zarray = [|                    
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    let mmin = if points.Length > 0 then BitConverter.ToDouble(buf, x) else 0.0
    let mmax = if points.Length > 0 then BitConverter.ToDouble(buf, x + 8) else 0.0
    let marray = [|
        if points.Length > 0 then        
            x <- x + 16
            for i = 1 to numPoints do
                yield BitConverter.ToDouble(buf, x)
                x <- x + 8
    |]
    MultiPointZ(xmin, ymin, xmax, ymax, numPoints, points, zmin, zmax, zarray, mmin, mmax, marray)

type PolylineZ (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, zmin, zmax, zarray, mmin, mmax, marray) = 
    inherit PolylineM(xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, mmin, mmax, marray)
    member this.ZMin with get() = zmin
    member this.ZMax with get() = zmax
    member this.ZArray with get() = zarray

let private readPolylineZ (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)
    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]

    let zmin = BitConverter.ToDouble(buf, x)
    let zmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let zarray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    
    let mmin = BitConverter.ToDouble(buf, x)
    let mmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let marray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    PolylineZ (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, zmin, zmax, zarray, mmin, mmax, marray)

type PolygonZ (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, zmin, zmax, zarray, mmin, mmax, marray) =
    inherit PolylineZ (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, zmin, zmax, zarray, mmin, mmax, marray)

let private readPolygonZ (buf:byte[]) =
    let xmin = BitConverter.ToDouble(buf, 4)
    let ymin = BitConverter.ToDouble(buf, 12)
    let xmax = BitConverter.ToDouble(buf, 20)
    let ymax = BitConverter.ToDouble(buf, 28)

    let numParts = BitConverter.ToInt32(buf, 36)
    let numPoints = BitConverter.ToInt32(buf, 40)
    let mutable x = 44
    let parts = [|
        for i = 1 to numParts do
            yield BitConverter.ToInt32(buf, x)
            x <- x + 4
    |]
    let points = [|
        for i = 1 to numPoints do
            yield readPoint buf x
            x <- x + 16
    |]

    let zmin = BitConverter.ToDouble(buf, x)
    let zmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let zarray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    
    let mmin = BitConverter.ToDouble(buf, x)
    let mmax = BitConverter.ToDouble(buf, x + 8)
    
    x <- x + 16
    let marray = [|
        for i = 1 to numPoints do
            yield BitConverter.ToDouble(buf, x)
            x <- x + 8
    |]
    PolygonZ (xmin, ymin, xmax, ymax, numParts, numPoints, parts, points, zmin, zmax, zarray, mmin, mmax, marray)
        
let private read_multipatch () = NullShape()

type RecordHeader = { Number: Int32; Length: Int32; }

let private readRecordHeader (rd:BinaryReader) =    
    let buf = rd.ReadBytes 8
    let num = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[0..4]))
    let len = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[4..]))
    { Number = num; Length = len; }

let private readRecordContent (rd:BinaryReader) len =
    let buf = rd.ReadBytes (len*2)
    match BitConverter.ToInt32(buf, 0) with
    | 0 -> NullShape() :> Shape
    | 1 -> readPoint buf 4 :> Shape
    | 3 -> readPolyline buf :> Shape
    | 5 -> readPolygon buf :> Shape
    | 8 -> readMultiPoint buf :> Shape
    | 11 -> readPointZ buf :> Shape
    | 13 -> readPolylineZ buf :> Shape
    | 15 -> readPolygonZ buf :> Shape
    | 18 -> readMultiPointZ buf :> Shape
    | 21 -> readPointm buf :> Shape
    | 23 -> readPolylineM buf :> Shape
    | 25 -> readPolygonM buf :> Shape
    | 28 -> readMultiPointM buf :> Shape
    | 31 -> read_multipatch() :> Shape
    | _ -> failwith "Unknown shape type"

let readRecords shp =
    seq {    
        let h = readHeader shp
        use fs = File.OpenRead shp
        fs.Seek(100L, SeekOrigin.Begin) |> ignore
        use rd = new BinaryReader(fs)        
        while rd.PeekChar() > -1 do
            let { Number = _; Length = len } = readRecordHeader rd
            yield readRecordContent rd len            
        rd.Close()
    }

type IndexRecord = { Offset: int32; Length: int32 }

let readIndexRecord shx num =
    use fs = File.OpenRead shx    
    use rd = new BinaryReader(fs)
    fs.Seek(int64(100 + num * 8), SeekOrigin.Begin) |> ignore
    let buf = rd.ReadBytes 8
    let off = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[0..4]))
    let len = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[4..]))
    { Offset = off; Length = len }

let readIndex shx =
    seq {
        use fs = File.OpenRead shx    
        use rd = new BinaryReader(fs)
        fs.Seek(100L, SeekOrigin.Begin) |> ignore
        while rd.PeekChar() > -1 do
            let buf = rd.ReadBytes 8
            let off = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[0..4]))
            let len = BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpan<byte>(buf.[4..]))
            yield { Offset = off; Length = len }
    }
    
let readRecord shp (offset:int32) =
    use fs = File.OpenRead shp
    use rd = new BinaryReader(fs)
    fs.Seek(int64(offset * 2), SeekOrigin.Begin) |> ignore
    let { Number = _; Length = len } = readRecordHeader rd
    readRecordContent rd len